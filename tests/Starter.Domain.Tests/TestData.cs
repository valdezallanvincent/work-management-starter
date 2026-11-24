using Starter.Domain.Projects;
using Starter.Domain.WorkItems;

namespace Starter.Domain.Tests;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);
    public static readonly Guid OwnerId = Guid.NewGuid();
    public static readonly Guid MemberId = Guid.NewGuid();

    public static Project ActiveProject() => Project.Create(ProjectKey.Create("OPS"), "Operations", null, OwnerId);

    public static WorkItem OpenItem(Project? project = null) =>
        WorkItem.Create(project ?? ActiveProject(), "Replace depot scanners", null, WorkItemPriority.Medium, OwnerId, null);

    public static WorkItem ItemIn(WorkItemStatus status)
    {
        var item = OpenItem();
        item.AssignTo(MemberId, OwnerId, Now);
        WorkItemStatus[] path = status switch
        {
            WorkItemStatus.InProgress => [WorkItemStatus.InProgress],
            WorkItemStatus.InReview => [WorkItemStatus.InProgress, WorkItemStatus.InReview],
            WorkItemStatus.Completed => [WorkItemStatus.InProgress, WorkItemStatus.InReview, WorkItemStatus.Completed],
            WorkItemStatus.Cancelled => [WorkItemStatus.Cancelled],
            _ => [],
        };
        foreach (var next in path)
        {
            item.ChangeStatus(next, Now);
        }

        item.ClearDomainEvents();
        return item;
    }
}
