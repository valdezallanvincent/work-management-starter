namespace Starter.Infrastructure.Messaging;

/// <summary>
/// The message can never succeed (unknown type, unreadable payload), so retrying is pointless and
/// it is dead-lettered straight away.
/// </summary>
public sealed class PoisonMessageException(string message, Exception? innerException = null)
    : Exception(message, innerException);
