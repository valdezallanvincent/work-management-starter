namespace Starter.Domain.WorkItems;

/// <summary>
/// The workflow is a small explicit table rather than a state-machine library:
/// it is easy to read, easy to test exhaustively, and the client can ask which moves are allowed.
/// </summary>
public static class WorkItemStatusTransitions
{
    private static readonly Dictionary<WorkItemStatus, WorkItemStatus[]> Allowed = new()
    {
        [WorkItemStatus.Open] = [WorkItemStatus.InProgress, WorkItemStatus.Cancelled],
        [WorkItemStatus.InProgress] = [WorkItemStatus.InReview, WorkItemStatus.Open, WorkItemStatus.Cancelled],
        [WorkItemStatus.InReview] = [WorkItemStatus.Completed, WorkItemStatus.InProgress, WorkItemStatus.Cancelled],
        [WorkItemStatus.Completed] = [WorkItemStatus.Open],
        [WorkItemStatus.Cancelled] = [WorkItemStatus.Open],
    };

    public static IReadOnlyList<WorkItemStatus> From(WorkItemStatus current) => Allowed[current];

    public static bool IsAllowed(WorkItemStatus from, WorkItemStatus to) => Allowed[from].Contains(to);

    public static bool IsClosed(WorkItemStatus status) =>
        status is WorkItemStatus.Completed or WorkItemStatus.Cancelled;
}
