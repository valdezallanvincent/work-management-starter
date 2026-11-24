using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Starter.Application.Common.Messaging;

namespace Starter.Infrastructure.Messaging;

public sealed record SerializedMessage(string Type, string Payload);

/// <summary>
/// Maps stable wire names to CLR types and their handler. Wire names are versioned and decoupled
/// from class names so a type can be renamed or moved without breaking messages already in flight.
/// </summary>
public sealed class MessageTypeRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, Registration> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, string> _byType = [];

    public MessageTypeRegistry Register<TMessage>(string wireName)
        where TMessage : class
    {
        _byName.Add(wireName, new Registration(
            typeof(TMessage),
            typeof(IMessageHandler<TMessage>).Name + ":" + typeof(TMessage).Name,
            (services, message, context, ct) =>
                services.GetRequiredService<IMessageHandler<TMessage>>().HandleAsync((TMessage)message, context, ct)));
        _byType.Add(typeof(TMessage), wireName);
        return this;
    }

    public SerializedMessage Serialize(object message)
    {
        if (!_byType.TryGetValue(message.GetType(), out var wireName))
        {
            // Failing the save is intentional: an unregistered event would otherwise be silently lost.
            throw new InvalidOperationException($"Message type {message.GetType().Name} is not registered for the outbox.");
        }

        return new SerializedMessage(wireName, JsonSerializer.Serialize(message, message.GetType(), JsonOptions));
    }

    internal Registration Resolve(string wireName) =>
        _byName.TryGetValue(wireName, out var registration)
            ? registration
            : throw new PoisonMessageException($"No handler is registered for message type '{wireName}'.");

    internal static object Deserialize(Registration registration, string payload)
    {
        try
        {
            return JsonSerializer.Deserialize(payload, registration.ClrType, JsonOptions)
                ?? throw new PoisonMessageException("Message payload was empty.");
        }
        catch (JsonException exception)
        {
            throw new PoisonMessageException("Message payload could not be deserialized.", exception);
        }
    }

    internal sealed record Registration(
        Type ClrType,
        string ConsumerName,
        Func<IServiceProvider, object, MessageContext, CancellationToken, Task> Invoke);
}
