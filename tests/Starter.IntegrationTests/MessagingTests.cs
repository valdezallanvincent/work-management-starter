using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Starter.Domain.Users;
using Starter.Domain.WorkItems;
using Starter.Infrastructure.Messaging;

namespace Starter.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class MessagingTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private OutboxProcessor Processor => factory.Services.GetRequiredService<OutboxProcessor>();
    private MessageConsumer Consumer => factory.Services.GetRequiredService<MessageConsumer>();
    private MessageTypeRegistry Registry => factory.Services.GetRequiredService<MessageTypeRegistry>();

    [Fact]
    public async Task Assignment_writes_an_outbox_message_in_the_same_transaction_and_dispatch_notifies_the_assignee()
    {
        var manager = await factory.CreateUserAsync(UserRole.Manager);
        var assignee = await factory.CreateUserAsync(UserRole.Member);
        using var client = await factory.SignInAsync(manager);
        var item = await client.CreateWorkItemAsync((await client.CreateProjectAsync()).Id);

        await client.PutAsJsonAsync($"/api/v1/work-items/{item.Id}/assignee", new { assigneeId = assignee.Id, item.RowVersion }, Ct);

        var pending = await factory.WithDbAsync(db => db.OutboxMessages.AsNoTracking()
            .Where(m => m.ProcessedAtUtc == null && m.Payload.Contains(item.Id.ToString())).ToListAsync(Ct));
        var message = Assert.Single(pending);
        Assert.Equal("work-item-assigned.v1", message.Type);
        Assert.NotNull(message.CorrelationId);

        await Processor.ProcessBatchAsync(Ct);

        var notification = await factory.WithDbAsync(db => db.Notifications.AsNoTracking().SingleAsync(n => n.RecipientId == assignee.Id, Ct));
        Assert.Equal(item.Id, notification.WorkItemId);
        var processed = await factory.WithDbAsync(db => db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == message.Id, Ct));
        Assert.NotNull(processed.ProcessedAtUtc);
    }

    [Fact]
    public async Task A_redelivered_message_is_processed_only_once()
    {
        var assignee = await factory.CreateUserAsync(UserRole.Member);
        var incoming = Incoming(new WorkItemAssigned(Guid.NewGuid(), Guid.NewGuid(), "Duplicate check", assignee.Id, null, DateTimeOffset.UtcNow));

        var first = await Consumer.ConsumeAsync(incoming, Ct);
        var second = await Consumer.ConsumeAsync(incoming with { DeliveryAttempt = 2 }, Ct);

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(1, await factory.WithDbAsync(db => db.Notifications.CountAsync(n => n.RecipientId == assignee.Id, Ct)));
    }

    [Fact]
    public async Task Concurrent_deliveries_of_the_same_message_produce_one_side_effect()
    {
        var assignee = await factory.CreateUserAsync(UserRole.Member);
        var incoming = Incoming(new WorkItemAssigned(Guid.NewGuid(), Guid.NewGuid(), "Race", assignee.Id, null, DateTimeOffset.UtcNow));

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Consumer.ConsumeAsync(incoming, Ct)));

        Assert.Equal(1, results.Count(processed => processed));
        Assert.Equal(1, await factory.WithDbAsync(db => db.Notifications.CountAsync(n => n.RecipientId == assignee.Id, Ct)));
    }

    [Fact]
    public async Task Messages_that_can_never_succeed_are_dead_lettered_without_retrying()
    {
        var id = await AddOutboxMessageAsync(new SerializedMessage("unknown-message.v1", "{}"));

        await Processor.ProcessBatchAsync(Ct);

        var message = await factory.WithDbAsync(db => db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == id, Ct));
        Assert.NotNull(message.DeadLetteredAtUtc);
        Assert.Equal(1, message.AttemptCount);
        Assert.Contains("No handler", message.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failing_messages_are_retried_later_with_backoff()
    {
        // The recipient does not exist, so saving the notification violates a foreign key: a failure
        // that is not the message's fault in principle and is therefore retried, not dead-lettered.
        var payload = Registry.Serialize(new WorkItemAssigned(Guid.NewGuid(), Guid.NewGuid(), "Orphan", Guid.NewGuid(), null, DateTimeOffset.UtcNow));
        var id = await AddOutboxMessageAsync(payload);

        await Processor.ProcessBatchAsync(Ct);

        var message = await factory.WithDbAsync(db => db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == id, Ct));
        Assert.Null(message.ProcessedAtUtc);
        Assert.Null(message.DeadLetteredAtUtc);
        Assert.Equal(1, message.AttemptCount);
        Assert.True(message.NextAttemptAtUtc > DateTimeOffset.UtcNow);
        Assert.NotNull(message.LastError);
    }

    private IncomingMessage Incoming(object payload)
    {
        var serialized = Registry.Serialize(payload);
        return new IncomingMessage(Guid.NewGuid(), serialized.Type, serialized.Payload, "test-correlation", 1);
    }

    private Task<Guid> AddOutboxMessageAsync(SerializedMessage message) =>
        factory.WithDbAsync(async db =>
        {
            var outbox = OutboxMessage.Create(message, DateTimeOffset.UtcNow, null);
            db.OutboxMessages.Add(outbox);
            await db.SaveChangesAsync(Ct);
            return outbox.Id;
        });
}
