namespace Starter.Application.Dashboard;

/// <summary>
/// Every figure is computed from current data at request time; nothing here is estimated or seeded.
/// </summary>
public sealed record DashboardDto(
    int ActiveProjectCount,
    int OpenWorkItemCount,
    int AssignedToMeOpenCount,
    int OverdueCount,
    int CompletedLast7DaysCount,
    IReadOnlyList<StatusCountDto> ByStatus,
    IReadOnlyList<PriorityCountDto> OpenByPriority);

public sealed record StatusCountDto(Domain.WorkItems.WorkItemStatus Status, int Count);

public sealed record PriorityCountDto(Domain.WorkItems.WorkItemPriority Priority, int Count);
