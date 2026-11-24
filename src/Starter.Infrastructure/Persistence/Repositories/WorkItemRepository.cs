using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Paging;
using Starter.Application.Common.Persistence;
using Starter.Application.Dashboard;
using Starter.Application.WorkItems;
using Starter.Domain.Projects;
using Starter.Domain.WorkItems;

namespace Starter.Infrastructure.Persistence.Repositories;

internal sealed class WorkItemRepository(StarterDbContext db) : IWorkItemRepository
{
    private static readonly WorkItemStatus[] ClosedStatuses = [WorkItemStatus.Completed, WorkItemStatus.Cancelled];

    public Task<WorkItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.WorkItems.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        db.WorkItems.AnyAsync(w => w.Id == id, cancellationToken);

    public async Task<WorkItemDetailsDto?> FindDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await (
                from w in db.WorkItems.AsNoTracking()
                join p in db.Projects on w.ProjectId equals p.Id
                join reporter in db.Users on w.ReporterId equals reporter.Id
                join assignee in db.Users on w.AssigneeId equals assignee.Id into assignees
                from assignee in assignees.DefaultIfEmpty()
                join modifier in db.Users on w.ModifiedById equals modifier.Id into modifiers
                from modifier in modifiers.DefaultIfEmpty()
                where w.Id == id
                select new
                {
                    WorkItem = w,
                    ProjectKey = p.Key,
                    ProjectName = p.Name,
                    ProjectOwnerId = p.OwnerId,
                    ProjectStatus = p.Status,
                    ReporterName = reporter.DisplayName,
                    AssigneeName = assignee != null ? assignee.DisplayName : null,
                    ModifiedByName = modifier != null ? modifier.DisplayName : null,
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var item = row.WorkItem;
        var archived = row.ProjectStatus == ProjectStatus.Archived;
        return new WorkItemDetailsDto(
            item.Id, item.ProjectId, row.ProjectKey.Value, row.ProjectName, row.ProjectOwnerId, archived, item.Title, item.Description, item.Status, item.Priority,
            item.ReporterId, row.ReporterName, item.AssigneeId, row.AssigneeName, item.DueDate, item.CompletedAtUtc,
            item.CreatedAtUtc, item.ModifiedAtUtc, row.ModifiedByName,
            archived ? [] : WorkItemStatusTransitions.From(item.Status),
            RowVersion.Encode(item.RowVersion));
    }

    public Task<PagedResult<WorkItemSummaryDto>> SearchAsync(WorkItemQuery query, DateOnly today, CancellationToken cancellationToken)
    {
        var items = db.WorkItems.AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            items = items.Where(w => w.ProjectId == projectId);
        }

        if (query.Status is { Length: > 0 } statuses)
        {
            items = items.Where(w => statuses.Contains(w.Status));
        }

        if (query.Priority is { } priority)
        {
            items = items.Where(w => w.Priority == priority);
        }

        if (query.AssigneeId is { } assigneeId)
        {
            items = items.Where(w => w.AssigneeId == assigneeId);
        }

        if (query.Overdue)
        {
            items = items.Where(w => w.DueDate < today && !ClosedStatuses.Contains(w.Status));
        }

        if (query.NormalizedSearch() is { } search)
        {
            items = items.Where(w => w.Title.Contains(search));
        }

        // Join first, then sort: ordering applied before a join is not guaranteed to survive it.
        // A single SQL statement; assignee and project names never trigger per-row queries.
        var joined =
            from w in items
            join p in db.Projects on w.ProjectId equals p.Id
            join assignee in db.Users on w.AssigneeId equals assignee.Id into assignees
            from assignee in assignees.DefaultIfEmpty()
            select new { WorkItem = w, ProjectKey = p.Key, AssigneeName = assignee != null ? assignee.DisplayName : null };

        // Every sort ends with Id so that pages are stable when the primary sort value has ties.
        var direction = query.SortDirection;
        var sorted = query.SortBy?.ToLowerInvariant() switch
        {
            "title" => joined.OrderByDirection(x => x.WorkItem.Title, direction),
            "priority" => joined.OrderByDirection(x => x.WorkItem.Priority, direction),
            "status" => joined.OrderByDirection(x => x.WorkItem.Status, direction),
            "duedate" => joined.OrderByDirection(x => x.WorkItem.DueDate, direction),
            "assignee" => joined.OrderByDirection(x => x.AssigneeName, direction),
            "modifiedat" => joined.OrderByDirection(x => x.WorkItem.ModifiedAtUtc ?? x.WorkItem.CreatedAtUtc, direction),
            "createdat" => joined.OrderByDirection(x => x.WorkItem.CreatedAtUtc, direction),
            _ => joined.OrderByDescending(x => x.WorkItem.CreatedAtUtc),
        };

        var projected = sorted
            .ThenBy(x => x.WorkItem.Id)
            .Select(x => new WorkItemSummaryDto(
                x.WorkItem.Id,
                x.WorkItem.ProjectId,
                x.ProjectKey.Value,
                x.WorkItem.Title,
                x.WorkItem.Status,
                x.WorkItem.Priority,
                x.WorkItem.AssigneeId,
                x.AssigneeName,
                x.WorkItem.DueDate,
                x.WorkItem.CreatedAtUtc,
                x.WorkItem.ModifiedAtUtc));

        return projected.ToPagedResultAsync(query, cancellationToken);
    }

    public Task<int> CountInProgressAsync(Guid projectId, CancellationToken cancellationToken) =>
        db.WorkItems.CountAsync(
            w => w.ProjectId == projectId && (w.Status == WorkItemStatus.InProgress || w.Status == WorkItemStatus.InReview),
            cancellationToken);

    public Task<int> CancelOpenWorkItemsAsync(Guid projectId, DateTimeOffset now, Guid? modifiedById, CancellationToken cancellationToken) =>
        // ExecuteUpdate bypasses the change tracker and interceptors, so audit columns are set here.
        db.WorkItems
            .Where(w => w.ProjectId == projectId && w.Status == WorkItemStatus.Open)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(w => w.Status, WorkItemStatus.Cancelled)
                    .SetProperty(w => w.ModifiedAtUtc, now)
                    .SetProperty(w => w.ModifiedById, modifiedById),
                cancellationToken);

    public async Task<DashboardDto> GetDashboardAsync(Guid userId, DateOnly today, CancellationToken cancellationToken)
    {
        var items = db.WorkItems.AsNoTracking();
        var weekAgo = new DateTimeOffset(today.AddDays(-7).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        // DbContext is not thread-safe, so these run sequentially. Each is a single indexed aggregate.
        var activeProjects = await db.Projects.CountAsync(p => p.Status == ProjectStatus.Active, cancellationToken);

        var byStatus = await items
            .GroupBy(w => w.Status)
            .Select(g => new StatusCountDto(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        var openByPriority = await items
            .Where(w => !ClosedStatuses.Contains(w.Status))
            .GroupBy(w => w.Priority)
            .Select(g => new PriorityCountDto(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        var assignedToMe = await items.CountAsync(
            w => w.AssigneeId == userId && !ClosedStatuses.Contains(w.Status), cancellationToken);

        var overdue = await items.CountAsync(
            w => w.DueDate < today && !ClosedStatuses.Contains(w.Status), cancellationToken);

        var completedRecently = await items.CountAsync(
            w => w.CompletedAtUtc >= weekAgo, cancellationToken);

        return new DashboardDto(
            activeProjects,
            openByPriority.Sum(p => p.Count),
            assignedToMe,
            overdue,
            completedRecently,
            Enum.GetValues<WorkItemStatus>().Select(s => new StatusCountDto(s, byStatus.FirstOrDefault(b => b.Status == s)?.Count ?? 0)).ToList(),
            Enum.GetValues<WorkItemPriority>().Select(p => new PriorityCountDto(p, openByPriority.FirstOrDefault(b => b.Priority == p)?.Count ?? 0)).ToList());
    }

    public void Add(WorkItem workItem) => db.WorkItems.Add(workItem);
}
