using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Starter.Application.Common.Security;
using Starter.Domain.Common;

namespace Starter.Infrastructure.Persistence.Interceptors;

internal sealed class AuditingInterceptor(ICurrentUser currentUser, TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            ApplyAuditValues(context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context)
        {
            ApplyAuditValues(context);
        }

        return base.SavingChanges(eventData, result);
    }

    private void ApplyAuditValues(DbContext context)
    {
        var now = timeProvider.GetUtcNow();
        var userId = currentUser.UserIdOrDefault;

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(e => e.CreatedAtUtc).CurrentValue = now;
                    entry.Property(e => e.CreatedById).CurrentValue = userId;
                    break;
                case EntityState.Modified:
                    entry.Property(e => e.ModifiedAtUtc).CurrentValue = now;
                    entry.Property(e => e.ModifiedById).CurrentValue = userId;
                    break;
            }
        }
    }
}
