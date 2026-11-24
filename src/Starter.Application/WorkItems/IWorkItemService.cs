using Starter.Application.Common.Paging;

namespace Starter.Application.WorkItems;

public interface IWorkItemService
{
    Task<PagedResult<WorkItemSummaryDto>> SearchAsync(WorkItemQuery query, CancellationToken cancellationToken);

    Task<WorkItemDetailsDto> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<WorkItemDetailsDto> CreateAsync(CreateWorkItemRequest request, CancellationToken cancellationToken);

    Task<WorkItemDetailsDto> UpdateAsync(Guid id, UpdateWorkItemRequest request, CancellationToken cancellationToken);

    Task<WorkItemDetailsDto> ChangePriorityAsync(Guid id, ChangeWorkItemPriorityRequest request, CancellationToken cancellationToken);

    Task<WorkItemDetailsDto> AssignAsync(Guid id, AssignWorkItemRequest request, CancellationToken cancellationToken);

    Task<WorkItemDetailsDto> ChangeStatusAsync(Guid id, ChangeWorkItemStatusRequest request, CancellationToken cancellationToken);
}
