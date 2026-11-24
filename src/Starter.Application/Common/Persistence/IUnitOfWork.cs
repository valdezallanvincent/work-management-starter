using System.Data;

namespace Starter.Application.Common.Persistence;

public interface IUnitOfWork
{
    /// <summary>
    /// Persists tracked changes in a single database transaction. Domain events raised by the
    /// changed entities are written to the outbox as part of the same transaction.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Runs <paramref name="operation"/> inside an explicit transaction. Use this only when a
    /// workflow needs more than one round trip to be atomic, or a stronger isolation level.
    /// The operation may be retried on transient failures, so it must load the data it changes.
    /// </summary>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken);
}
