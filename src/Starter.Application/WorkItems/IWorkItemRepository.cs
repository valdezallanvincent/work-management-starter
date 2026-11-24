using Starter.Application.Common.Paging;
using Starter.Application.Dashboard;
using Starter.Domain.WorkItems;

namespace Starter.Application.WorkItems;

public interface IWorkItemRepository
{
    Task<WorkItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    Task<WorkItemDetailsDto?> FindDetailsAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<WorkItemSummaryDto>> SearchAsync(WorkItemQuery query, DateOnly today, CancellationToken cancellationToken);

    Task<int> CountInProgressAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>Set-based update that runs immediately; call it inside a transaction with related changes.</summary>
    Task<int> CancelOpenWorkItemsAsync(Guid projectId, DateTimeOffset now, Guid? modifiedById, CancellationToken cancellationToken);

    Task<DashboardDto> GetDashboardAsync(Guid userId, DateOnly today, CancellationToken cancellationToken);

    void Add(WorkItem workItem);
}
