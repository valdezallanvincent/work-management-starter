using System.ComponentModel.DataAnnotations;
using Starter.Application.Common.Paging;
using Starter.Domain.WorkItems;

namespace Starter.Application.WorkItems;

public sealed record WorkItemSummaryDto(
    Guid Id,
    Guid ProjectId,
    string ProjectKey,
    string Title,
    WorkItemStatus Status,
    WorkItemPriority Priority,
    Guid? AssigneeId,
    string? AssigneeName,
    DateOnly? DueDate,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ModifiedAtUtc);

public sealed record WorkItemDetailsDto(
    Guid Id,
    Guid ProjectId,
    string ProjectKey,
    string ProjectName,
    Guid ProjectOwnerId,
    bool ProjectIsArchived,
    string Title,
    string? Description,
    WorkItemStatus Status,
    WorkItemPriority Priority,
    Guid ReporterId,
    string ReporterName,
    Guid? AssigneeId,
    string? AssigneeName,
    DateOnly? DueDate,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ModifiedAtUtc,
    string? ModifiedByName,
    IReadOnlyList<WorkItemStatus> AllowedTransitions,
    string RowVersion)
{
    /// <summary>What the current user may do; filled in by the service, which knows the caller.</summary>
    public WorkItemPermissions Permissions { get; init; } = WorkItemPermissions.None;
}

public sealed record WorkItemPermissions(bool CanEdit, bool CanChangeStatus, bool CanAssignOthers, bool CanSelfAssign)
{
    public static readonly WorkItemPermissions None = new(false, false, false, false);
}

public sealed record WorkItemQuery : PageQuery
{
    public Guid? ProjectId { get; init; }

    public WorkItemStatus[]? Status { get; init; }

    public WorkItemPriority? Priority { get; init; }

    public Guid? AssigneeId { get; init; }

    /// <summary>Shortcut for "assigned to the caller", resolved by the service.</summary>
    public bool AssignedToMe { get; init; }

    public bool Overdue { get; init; }
}

public sealed record CreateWorkItemRequest(
    Guid ProjectId,
    [Required, StringLength(WorkItem.TitleMaxLength, MinimumLength = 1)] string Title,
    [StringLength(WorkItem.DescriptionMaxLength)] string? Description,
    [EnumDataType(typeof(WorkItemPriority))] WorkItemPriority Priority,
    Guid? AssigneeId,
    DateOnly? DueDate);

public sealed record UpdateWorkItemRequest(
    [Required, StringLength(WorkItem.TitleMaxLength, MinimumLength = 1)] string Title,
    [StringLength(WorkItem.DescriptionMaxLength)] string? Description,
    [EnumDataType(typeof(WorkItemPriority))] WorkItemPriority Priority,
    DateOnly? DueDate,
    [Required] string RowVersion);

public sealed record AssignWorkItemRequest(Guid? AssigneeId, [Required] string RowVersion);

public sealed record ChangeWorkItemPriorityRequest(
    [EnumDataType(typeof(WorkItemPriority))] WorkItemPriority Priority,
    [Required] string RowVersion);

public sealed record ChangeWorkItemStatusRequest(
    [EnumDataType(typeof(WorkItemStatus))] WorkItemStatus Status,
    [Required] string RowVersion);
