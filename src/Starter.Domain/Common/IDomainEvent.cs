namespace Starter.Domain.Common;

/// <summary>
/// Marker for something that happened in the domain that other parts of the system may react to.
/// Events are persisted to the outbox in the same transaction as the change that raised them.
/// </summary>
#pragma warning disable CA1040 // A marker interface is intentional: it scopes which types the outbox accepts.
public interface IDomainEvent;
#pragma warning restore CA1040
