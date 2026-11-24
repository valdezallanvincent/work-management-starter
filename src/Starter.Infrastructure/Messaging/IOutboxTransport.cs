namespace Starter.Infrastructure.Messaging;

/// <summary>
/// Where the outbox dispatcher hands messages. Throwing means "not delivered, retry later".
/// </summary>
public interface IOutboxTransport
{
    Task SendAsync(OutboxMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// Local transport: the outbox table is the queue and handlers run in this process. Durability comes
/// from SQL Server, not from memory. A crash after the handler commits but before the outbox row is
/// marked processed causes a redelivery, which the idempotent consumer absorbs.
/// </summary>
internal sealed class InProcessOutboxTransport(MessageConsumer consumer) : IOutboxTransport
{
    public Task SendAsync(OutboxMessage message, CancellationToken cancellationToken) =>
        consumer.ConsumeAsync(
            new IncomingMessage(message.Id, message.Type, message.Payload, message.CorrelationId, message.AttemptCount + 1),
            cancellationToken);
}
