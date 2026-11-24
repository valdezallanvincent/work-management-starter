namespace Starter.Infrastructure.Messaging;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    public string Type { get; private set; } = null!;

    public string Payload { get; private set; } = null!;

    public string? CorrelationId { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public DateTimeOffset NextAttemptAtUtc { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    public DateTimeOffset? DeadLetteredAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public static OutboxMessage Create(SerializedMessage message, DateTimeOffset now, string? correlationId) => new()
    {
        Id = Guid.CreateVersion7(),
        Type = message.Type,
        Payload = message.Payload,
        CorrelationId = correlationId,
        OccurredAtUtc = now,
        NextAttemptAtUtc = now,
    };

    public void MarkProcessed(DateTimeOffset now)
    {
        ProcessedAtUtc = now;
        LastError = null;
    }

    public void RecordFailure(string error, DateTimeOffset now, int maxAttempts, TimeSpan baseDelay)
    {
        AttemptCount++;
        LastError = error.Length > 2000 ? error[..2000] : error;

        if (AttemptCount >= maxAttempts)
        {
            DeadLetteredAtUtc = now;
            return;
        }

        // Exponential backoff with a ceiling: 1x, 2x, 4x ... capped at 64x the base delay.
        var factor = Math.Pow(2, Math.Min(AttemptCount - 1, 6));
        NextAttemptAtUtc = now + (baseDelay * factor);
    }

    public void DeadLetter(string error, DateTimeOffset now)
    {
        AttemptCount++;
        LastError = error.Length > 2000 ? error[..2000] : error;
        DeadLetteredAtUtc = now;
    }
}
