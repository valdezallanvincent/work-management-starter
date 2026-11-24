namespace Starter.Infrastructure.Messaging;

/// <summary>
/// Inbox record for idempotent consumers: one row per message per consumer, written in the same
/// transaction as the consumer's side effects.
/// </summary>
public sealed class ProcessedMessage
{
    public Guid MessageId { get; init; }

    public string Consumer { get; init; } = null!;

    public DateTimeOffset ProcessedAtUtc { get; init; }
}
