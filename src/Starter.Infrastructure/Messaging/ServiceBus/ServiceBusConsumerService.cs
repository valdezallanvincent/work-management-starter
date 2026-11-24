using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Starter.Infrastructure.Messaging.ServiceBus;

/// <summary>
/// Receives from the queue in peek-lock mode. Success completes the message; a transient failure
/// abandons it so the broker redelivers it until the queue's MaxDeliveryCount moves it to the
/// dead-letter queue. Messages that can never succeed are dead-lettered immediately.
/// </summary>
internal sealed class ServiceBusConsumerService(
    ServiceBusClient client,
    MessageConsumer consumer,
    IOptions<MessagingOptions> options,
    ILogger<ServiceBusConsumerService> logger) : BackgroundService
{
    private ServiceBusProcessor? _processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value.ServiceBus;
        _processor = client.CreateProcessor(settings.QueueName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = settings.MaxConcurrentCalls,
            ReceiveMode = ServiceBusReceiveMode.PeekLock,
        });

        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += HandleErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);
        logger.LogInformation("Service Bus consumer listening on {Queue}", settings.QueueName);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        var message = args.Message;
        if (!Guid.TryParse(message.MessageId, out var messageId))
        {
            await args.DeadLetterMessageAsync(message, "InvalidMessageId", "MessageId is not a GUID.", args.CancellationToken);
            return;
        }

        try
        {
            await consumer.ConsumeAsync(
                new IncomingMessage(messageId, message.Subject, message.Body.ToString(), message.CorrelationId, message.DeliveryCount),
                args.CancellationToken);
            await args.CompleteMessageAsync(message, args.CancellationToken);
        }
        catch (PoisonMessageException exception)
        {
            logger.LogError(exception, "Dead-lettering message {MessageId}", messageId);
            await args.DeadLetterMessageAsync(message, "PoisonMessage", exception.Message, args.CancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Message {MessageId} failed on delivery {DeliveryCount}; abandoning for redelivery", messageId, message.DeliveryCount);
            await args.AbandonMessageAsync(message, cancellationToken: args.CancellationToken);
        }
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception, "Service Bus processor error from {Source} on {Entity}", args.ErrorSource, args.EntityPath);
        return Task.CompletedTask;
    }
}
