using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Messaging;
using Starter.Infrastructure.Persistence;

namespace Starter.Infrastructure.Messaging;

public sealed record IncomingMessage(Guid MessageId, string Type, string Payload, string? CorrelationId, int DeliveryAttempt);

/// <summary>
/// Shared consume pipeline for every transport: deduplicate, invoke the handler, and commit the
/// handler's changes together with the processed-message record. That single transaction is what
/// turns at-least-once delivery into an effectively-once side effect.
/// </summary>
public sealed class MessageConsumer(
    IServiceScopeFactory scopeFactory,
    MessageTypeRegistry registry,
    TimeProvider timeProvider,
    ILogger<MessageConsumer> logger)
{
    public async Task<bool> ConsumeAsync(IncomingMessage message, CancellationToken cancellationToken)
    {
        var registration = registry.Resolve(message.Type);
        var payload = MessageTypeRegistry.Deserialize(registration, message.Payload);

        using var logScope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["MessageId"] = message.MessageId,
            ["MessageType"] = message.Type,
            ["CorrelationId"] = message.CorrelationId,
        });

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StarterDbContext>();

        var alreadyProcessed = await db.ProcessedMessages.AnyAsync(
            m => m.MessageId == message.MessageId && m.Consumer == registration.ConsumerName, cancellationToken);
        if (alreadyProcessed)
        {
            logger.LogInformation("Duplicate message skipped (attempt {Attempt})", message.DeliveryAttempt);
            return false;
        }

        var context = new MessageContext(message.MessageId, message.CorrelationId, message.DeliveryAttempt);
        await registration.Invoke(scope.ServiceProvider, payload, context, cancellationToken);

        db.ProcessedMessages.Add(new ProcessedMessage
        {
            MessageId = message.MessageId,
            Consumer = registration.ConsumerName,
            ProcessedAtUtc = timeProvider.GetUtcNow(),
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (SqlServerErrors.IsUniqueViolation(exception))
        {
            // Another worker processed the same message concurrently and committed first.
            // Our transaction rolled back, so the side effect exists exactly once.
            logger.LogInformation("Message processed concurrently by another consumer; changes discarded");
            return false;
        }

        logger.LogDebug("Message processed");
        return true;
    }
}
