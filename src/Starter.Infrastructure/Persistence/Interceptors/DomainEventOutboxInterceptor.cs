using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Starter.Domain.Common;
using Starter.Infrastructure.Messaging;

namespace Starter.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Converts domain events into outbox rows inside the same SaveChanges call, so the business change
/// and the intent to publish are committed atomically. Nothing is sent to a broker here.
/// </summary>
internal sealed class DomainEventOutboxInterceptor(MessageTypeRegistry registry, TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            WriteOutboxMessages(context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context)
        {
            WriteOutboxMessages(context);
        }

        return base.SavingChanges(eventData, result);
    }

    private void WriteOutboxMessages(DbContext context)
    {
        var entities = context.ChangeTracker.Entries<Entity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

        if (entities.Count == 0)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var correlationId = Activity.Current?.TraceId.ToString();

        foreach (var entity in entities)
        {
            foreach (var domainEvent in entity.DomainEvents)
            {
                context.Add(OutboxMessage.Create(registry.Serialize(domainEvent), now, correlationId));
            }

            entity.ClearDomainEvents();
        }
    }
}
