using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Infrastructure.Persistence;

namespace Starter.Infrastructure.Messaging;

/// <summary>
/// Polls the outbox and hands pending messages to the configured transport.
/// Rows are claimed with UPDLOCK + READPAST inside a transaction, so several API instances can run
/// the dispatcher without picking up the same batch. Delivery is still at-least-once overall.
/// </summary>
public sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IOptions<MessagingOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        logger.LogInformation("Outbox processor started using {Transport} transport", settings.Transport);

        using var timer = new PeriodicTimer(settings.PollingInterval, timeProvider);
        do
        {
            try
            {
                // Drain quickly while there is a backlog, then fall back to the polling interval.
                while (await ProcessBatchAsync(stoppingToken) == settings.BatchSize && !stoppingToken.IsCancellationRequested)
                {
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // Database outages must not kill the hosted service; the next tick retries.
                logger.LogError(exception, "Outbox batch failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    /// <summary>Processes one batch and returns how many messages were attempted.</summary>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StarterDbContext>();
        var transport = scope.ServiceProvider.GetRequiredService<IOutboxTransport>();

        var attempted = 0;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async ct =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var now = timeProvider.GetUtcNow();
            var batch = await db.OutboxMessages
                .FromSql($"""
                    SELECT TOP ({settings.BatchSize}) *
                    FROM OutboxMessages WITH (UPDLOCK, READPAST, ROWLOCK)
                    WHERE ProcessedAtUtc IS NULL AND DeadLetteredAtUtc IS NULL AND NextAttemptAtUtc <= {now}
                    ORDER BY OccurredAtUtc
                    """)
                .ToListAsync(ct);

            foreach (var message in batch)
            {
                await DispatchAsync(transport, message, settings, ct);
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            attempted = batch.Count;
        }, cancellationToken);

        return attempted;
    }

    private async Task DispatchAsync(IOutboxTransport transport, OutboxMessage message, MessagingOptions settings, CancellationToken cancellationToken)
    {
        using var logScope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["MessageId"] = message.Id,
            ["MessageType"] = message.Type,
            ["CorrelationId"] = message.CorrelationId,
        });

        try
        {
            await transport.SendAsync(message, cancellationToken);
            message.MarkProcessed(timeProvider.GetUtcNow());
        }
        catch (PoisonMessageException exception)
        {
            message.DeadLetter(exception.Message, timeProvider.GetUtcNow());
            logger.LogError(exception, "Message dead-lettered: it cannot be processed");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            message.RecordFailure(exception.Message, timeProvider.GetUtcNow(), settings.MaxAttempts, settings.RetryBaseDelay);
            if (message.DeadLetteredAtUtc is not null)
            {
                logger.LogError(exception, "Message dead-lettered after {Attempts} attempts", message.AttemptCount);
            }
            else
            {
                logger.LogWarning(exception, "Message delivery failed (attempt {Attempt}); retry at {NextAttempt}", message.AttemptCount, message.NextAttemptAtUtc);
            }
        }
    }
}
