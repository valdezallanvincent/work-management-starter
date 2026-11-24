using Azure.Messaging.ServiceBus;

namespace Starter.Infrastructure.Messaging.ServiceBus;

/// <summary>
/// Publishes outbox rows to Service Bus. MessageId is the outbox id, so if a send succeeds but the
/// outbox update does not, the resend can be dropped by the queue's duplicate detection (when
/// enabled) and is otherwise absorbed by the idempotent consumer.
/// </summary>
internal sealed class ServiceBusOutboxTransport(ServiceBusSender sender) : IOutboxTransport
{
    public Task SendAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var serviceBusMessage = new ServiceBusMessage(BinaryData.FromString(message.Payload))
        {
            MessageId = message.Id.ToString(),
            CorrelationId = message.CorrelationId,
            Subject = message.Type,
            ContentType = "application/json",
        };

        return sender.SendMessageAsync(serviceBusMessage, cancellationToken);
    }
}
