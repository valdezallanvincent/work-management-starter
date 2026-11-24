using System.Data;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Exceptions;
using Starter.Application.Common.Persistence;

namespace Starter.Infrastructure.Persistence;

internal sealed class UnitOfWork(StarterDbContext dbContext) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException("The record was changed by another user. Reload it and apply your changes again.");
        }
        catch (DbUpdateException exception) when (SqlServerErrors.IsUniqueViolation(exception))
        {
            // Services check uniqueness up front; this covers the race where two requests pass that check together.
            throw new ConflictException("conflict.duplicate", "A record with the same unique value already exists.");
        }
    }

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is not null)
        {
            await operation(cancellationToken);
            return;
        }

        // With EnableRetryOnFailure, user transactions must run inside the execution strategy so the
        // whole unit (not a single statement) is replayed after a transient failure such as a deadlock.
        var strategy = dbContext.Database.CreateExecutionStrategy();
        var attempt = 0;

        await strategy.ExecuteAsync(async ct =>
        {
            if (attempt++ > 0)
            {
                dbContext.ChangeTracker.Clear();
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync(isolationLevel, ct);
            await operation(ct);
            await transaction.CommitAsync(ct);
        }, cancellationToken);
    }
}
