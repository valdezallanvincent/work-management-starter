using Starter.Application.Common.Security;
using Starter.Application.WorkItems;

namespace Starter.Application.Dashboard;

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken cancellationToken);
}

internal sealed class DashboardService(
    IWorkItemRepository workItems,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IDashboardService
{
    public Task<DashboardDto> GetAsync(CancellationToken cancellationToken) =>
        workItems.GetDashboardAsync(
            currentUser.UserId,
            DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime),
            cancellationToken);
}
