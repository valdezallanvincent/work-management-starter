using System.Data;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Exceptions;
using Starter.Application.Common.Paging;
using Starter.Application.Common.Persistence;
using Starter.Application.Common.Security;
using Starter.Application.Projects;
using Starter.Application.Users;
using Starter.Domain.Common;
using Starter.Domain.Projects;
using Starter.Domain.WorkItems;

namespace Starter.Application.WorkItems;

internal sealed class WorkItemService(
    IWorkItemRepository workItems,
    IProjectRepository projects,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<WorkItemService> logger) : IWorkItemService
{
    public Task<PagedResult<WorkItemSummaryDto>> SearchAsync(WorkItemQuery query, CancellationToken cancellationToken)
    {
        if (query.AssignedToMe)
        {
            query = query with { AssigneeId = currentUser.UserId };
        }

        return workItems.SearchAsync(query, Today(), cancellationToken);
    }

    public async Task<WorkItemDetailsDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var details = await workItems.FindDetailsAsync(id, cancellationToken) ?? throw new NotFoundException("Work item", id);
        return details with { Permissions = PermissionsFor(details) };
    }

    private WorkItemPermissions PermissionsFor(WorkItemDetailsDto item)
    {
        if (item.ProjectIsArchived)
        {
            return WorkItemPermissions.None;
        }

        var open = !WorkItemStatusTransitions.IsClosed(item.Status);
        return new WorkItemPermissions(
            CanEdit: open && AccessRules.CanEditWorkItem(currentUser, item.ProjectOwnerId, item.ReporterId, item.AssigneeId),
            CanChangeStatus: AccessRules.CanChangeStatus(currentUser, item.ProjectOwnerId, item.AssigneeId),
            CanAssignOthers: open && AccessRules.CanManageProject(currentUser, item.ProjectOwnerId),
            CanSelfAssign: open && AccessRules.CanAssign(currentUser, item.ProjectOwnerId, item.AssigneeId, currentUser.UserId));
    }

    /// <summary>
    /// The project is read under REPEATABLE READ so its shared lock is held until commit. An archive
    /// running at the same time must wait for this insert (and then cancels the new item), or this
    /// read waits for the archive and sees the archived status. Without the lock a work item could
    /// be created in a project that was archived a moment earlier.
    /// </summary>
    public async Task<WorkItemDetailsDto> CreateAsync(CreateWorkItemRequest request, CancellationToken cancellationToken)
    {
        if (request.AssigneeId is { } assigneeId)
        {
            await EnsureAssignableAsync(assigneeId, cancellationToken);
        }

        Guid workItemId = default;
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var project = await projects.GetByIdAsync(request.ProjectId, ct)
                    ?? throw new NotFoundException("Project", request.ProjectId);

                var workItem = WorkItem.Create(project, request.Title, request.Description, request.Priority, currentUser.UserId, request.DueDate);

                if (request.AssigneeId is not null)
                {
                    AccessRules.Demand(
                        AccessRules.CanAssign(currentUser, project, workItem, request.AssigneeId),
                        "Only the project owner or an administrator can assign work to other people.");
                    workItem.AssignTo(request.AssigneeId, currentUser.UserId, timeProvider.GetUtcNow());
                }

                workItems.Add(workItem);
                await unitOfWork.SaveChangesAsync(ct);
                workItemId = workItem.Id;
            },
            IsolationLevel.RepeatableRead,
            cancellationToken);

        logger.LogInformation("Work item {WorkItemId} created in project {ProjectId}", workItemId, request.ProjectId);
        return await GetAsync(workItemId, cancellationToken);
    }

    public async Task<WorkItemDetailsDto> UpdateAsync(Guid id, UpdateWorkItemRequest request, CancellationToken cancellationToken)
    {
        var (workItem, project) = await LoadForChangeAsync(id, request.RowVersion, cancellationToken);
        AccessRules.Demand(AccessRules.CanEditWorkItem(currentUser, project, workItem), "You cannot edit this work item.");

        workItem.UpdateDetails(request.Title, request.Description, request.DueDate);
        workItem.ChangePriority(request.Priority);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<WorkItemDetailsDto> ChangePriorityAsync(Guid id, ChangeWorkItemPriorityRequest request, CancellationToken cancellationToken)
    {
        var (workItem, project) = await LoadForChangeAsync(id, request.RowVersion, cancellationToken);
        AccessRules.Demand(AccessRules.CanEditWorkItem(currentUser, project, workItem), "You cannot change the priority of this work item.");

        workItem.ChangePriority(request.Priority);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<WorkItemDetailsDto> AssignAsync(Guid id, AssignWorkItemRequest request, CancellationToken cancellationToken)
    {
        var (workItem, project) = await LoadForChangeAsync(id, request.RowVersion, cancellationToken);
        AccessRules.Demand(
            AccessRules.CanAssign(currentUser, project, workItem, request.AssigneeId),
            "Only the project owner or an administrator can assign work to other people.");

        if (request.AssigneeId is { } assigneeId)
        {
            await EnsureAssignableAsync(assigneeId, cancellationToken);
        }

        workItem.AssignTo(request.AssigneeId, currentUser.UserId, timeProvider.GetUtcNow());

        // The WorkItemAssigned event is written to the outbox in this same SaveChanges call.
        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Work item {WorkItemId} assigned to {AssigneeId}", id, request.AssigneeId);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<WorkItemDetailsDto> ChangeStatusAsync(Guid id, ChangeWorkItemStatusRequest request, CancellationToken cancellationToken)
    {
        var (workItem, project) = await LoadForChangeAsync(id, request.RowVersion, cancellationToken);
        AccessRules.Demand(AccessRules.CanChangeStatus(currentUser, project, workItem), "Only the assignee or the project owner can change the status.");

        var previous = workItem.Status;
        workItem.ChangeStatus(request.Status, timeProvider.GetUtcNow());

        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Work item {WorkItemId} moved from {PreviousStatus} to {Status}", id, previous, request.Status);
        return await GetAsync(id, cancellationToken);
    }

    private async Task<(WorkItem WorkItem, Project Project)> LoadForChangeAsync(Guid id, string rowVersion, CancellationToken cancellationToken)
    {
        var workItem = await workItems.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Work item", id);
        var project = await projects.GetByIdAsync(workItem.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Project", workItem.ProjectId);

        project.EnsureCanAcceptWorkItems();
        RowVersion.EnsureMatches(workItem.RowVersion, rowVersion, "work item");
        return (workItem, project);
    }

    private async Task EnsureAssignableAsync(Guid assigneeId, CancellationToken cancellationToken)
    {
        if (!await users.IsActiveAsync(assigneeId, cancellationToken))
        {
            throw new DomainException("work_item.assignee_invalid", "Work can only be assigned to an active user.");
        }
    }

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}
