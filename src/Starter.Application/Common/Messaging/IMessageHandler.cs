namespace Starter.Application.Common.Messaging;

/// <summary>
/// Handles a message delivered from the outbox. Handlers only stage changes; the messaging
/// infrastructure saves them together with the idempotency record so a redelivered message
/// cannot apply its effect twice.
/// </summary>
public interface IMessageHandler<in TMessage>
    where TMessage : class
{
    Task HandleAsync(TMessage message, MessageContext context, CancellationToken cancellationToken);
}

public sealed record MessageContext(Guid MessageId, string? CorrelationId, int DeliveryAttempt);
