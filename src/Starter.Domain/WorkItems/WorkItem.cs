using Starter.Domain.Common;
using Starter.Domain.Projects;

namespace Starter.Domain.WorkItems;

public sealed class WorkItem : AuditableEntity
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 8000;

    private WorkItem(Guid id, Guid projectId, string title, string? description, WorkItemPriority priority, Guid reporterId, DateOnly? dueDate)
        : base(id)
    {
        ProjectId = projectId;
        Title = title;
        Description = description;
        Priority = priority;
        ReporterId = reporterId;
        DueDate = dueDate;
        Status = WorkItemStatus.Open;
    }

    private WorkItem()
    {
    }

    public Guid ProjectId { get; private set; }

    public string Title { get; private set; } = null!;

    public string? Description { get; private set; }

    public WorkItemStatus Status { get; private set; }

    public WorkItemPriority Priority { get; private set; }

    public Guid ReporterId { get; private set; }

    public Guid? AssigneeId { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool IsClosed => WorkItemStatusTransitions.IsClosed(Status);

    public static WorkItem Create(Project project, string title, string? description, WorkItemPriority priority, Guid reporterId, DateOnly? dueDate)
    {
        ArgumentNullException.ThrowIfNull(project);
        project.EnsureCanAcceptWorkItems();
        EnsureDefined(priority);

        return new WorkItem(
            Guid.CreateVersion7(),
            project.Id,
            Guard.RequiredText(title, TitleMaxLength, "work_item.title_invalid", "Title"),
            Guard.OptionalText(description, DescriptionMaxLength, "work_item.description_invalid", "Description"),
            priority,
            reporterId,
            dueDate);
    }

    public void UpdateDetails(string title, string? description, DateOnly? dueDate)
    {
        EnsureOpenForChanges();
        Title = Guard.RequiredText(title, TitleMaxLength, "work_item.title_invalid", "Title");
        Description = Guard.OptionalText(description, DescriptionMaxLength, "work_item.description_invalid", "Description");
        DueDate = dueDate;
    }

    public void ChangePriority(WorkItemPriority priority)
    {
        EnsureOpenForChanges();
        EnsureDefined(priority);
        Priority = priority;
    }

    public void AssignTo(Guid? assigneeId, Guid? assignedById, DateTimeOffset now)
    {
        EnsureOpenForChanges();

        if (AssigneeId == assigneeId)
        {
            return;
        }

        if (assigneeId is null && Status is WorkItemStatus.InProgress or WorkItemStatus.InReview)
        {
            throw new DomainException(
                "work_item.assignee_required",
                "Work in progress or in review must have an assignee. Move it back to Open before unassigning.");
        }

        AssigneeId = assigneeId;

        if (assigneeId is { } assignee)
        {
            Raise(new WorkItemAssigned(Id, ProjectId, Title, assignee, assignedById, now));
        }
    }

    public void ChangeStatus(WorkItemStatus newStatus, DateTimeOffset now)
    {
        if (Status == newStatus)
        {
            return;
        }

        if (!WorkItemStatusTransitions.IsAllowed(Status, newStatus))
        {
            throw new DomainException(
                "work_item.invalid_status_transition",
                $"A work item cannot move from {Status} to {newStatus}.");
        }

        if (newStatus is WorkItemStatus.InProgress or WorkItemStatus.InReview && AssigneeId is null)
        {
            throw new DomainException("work_item.assignee_required", $"Assign the work item before moving it to {newStatus}.");
        }

        Status = newStatus;
        CompletedAtUtc = newStatus == WorkItemStatus.Completed ? now : null;
    }

    private void EnsureOpenForChanges()
    {
        if (IsClosed)
        {
            throw new DomainException(
                "work_item.closed",
                $"{Status} work items cannot be modified. Reopen the work item first.");
        }
    }

    private static void EnsureDefined(WorkItemPriority priority)
    {
        if (!Enum.IsDefined(priority))
        {
            throw new DomainException("work_item.priority_invalid", "Priority is not valid.");
        }
    }
}
