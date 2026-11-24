using System.ComponentModel.DataAnnotations;

namespace Starter.Infrastructure.Messaging;

public enum MessageTransport
{
    /// <summary>Handlers run in this process, fed directly from the SQL outbox. No broker required.</summary>
    InProcess,

    /// <summary>The outbox publishes to an Azure Service Bus queue and a hosted consumer processes it.</summary>
    AzureServiceBus,
}

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    /// <summary>Turns the background dispatcher on or off; tests drive the dispatcher directly.</summary>
    public bool Enabled { get; set; } = true;

    public MessageTransport Transport { get; set; } = MessageTransport.InProcess;

    [Range(typeof(TimeSpan), "00:00:00.100", "00:10:00")]
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(2);

    [Range(1, 500)]
    public int BatchSize { get; set; } = 20;

    [Range(1, 50)]
    public int MaxAttempts { get; set; } = 5;

    [Range(typeof(TimeSpan), "00:00:00.100", "01:00:00")]
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(5);

    public ServiceBusOptions ServiceBus { get; set; } = new();
}

public sealed class ServiceBusOptions
{
    /// <summary>Supplied via environment variable or user secrets, never committed.</summary>
    public string? ConnectionString { get; set; }

    public string QueueName { get; set; } = "work-item-events";

    [Range(1, 64)]
    public int MaxConcurrentCalls { get; set; } = 4;
}
