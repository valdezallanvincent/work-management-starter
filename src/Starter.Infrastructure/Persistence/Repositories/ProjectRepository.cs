using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Paging;
using Starter.Application.Common.Persistence;
using Starter.Application.Projects;
using Starter.Domain.Common;
using Starter.Domain.Projects;
using Starter.Domain.WorkItems;

namespace Starter.Infrastructure.Persistence.Repositories;

internal sealed class ProjectRepository(StarterDbContext db) : IProjectRepository
{
    public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Projects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<bool> KeyExistsAsync(ProjectKey key, CancellationToken cancellationToken) =>
        db.Projects.AnyAsync(p => p.Key == key, cancellationToken);

    public async Task<ProjectDetailsDto?> FindDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        var project = await db.Projects.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new
            {
                Project = p,
                OwnerName = db.Users.Where(u => u.Id == p.OwnerId).Select(u => u.DisplayName).First(),
                CreatedByName = db.Users.Where(u => u.Id == p.CreatedById).Select(u => u.DisplayName).FirstOrDefault(),
                ModifiedByName = db.Users.Where(u => u.Id == p.ModifiedById).Select(u => u.DisplayName).FirstOrDefault(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (project is null)
        {
            return null;
        }

        // A second, grouped query is cheaper and clearer than five correlated COUNT subqueries.
        var counts = await db.WorkItems.AsNoTracking()
            .Where(w => w.ProjectId == id)
            .GroupBy(w => w.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count, cancellationToken);

        var byStatus = Enum.GetValues<WorkItemStatus>()
            .ToDictionary(s => s.ToString(), s => counts.GetValueOrDefault(s));

        var p = project.Project;
        return new ProjectDetailsDto(
            p.Id, p.Key.Value, p.Name, p.Description, p.Status, p.OwnerId, project.OwnerName, p.ArchivedAtUtc,
            p.CreatedAtUtc, project.CreatedByName, p.ModifiedAtUtc, project.ModifiedByName,
            byStatus, RowVersion.Encode(p.RowVersion));
    }

    public Task<PagedResult<ProjectSummaryDto>> SearchAsync(ProjectQuery query, CancellationToken cancellationToken)
    {
        var projects = db.Projects.AsNoTracking();

        if (query.NormalizedSearch() is { } search)
        {
            projects = TryParseKey(search) is { } key
                ? projects.Where(p => p.Name.Contains(search) || p.Key == key)
                : projects.Where(p => p.Name.Contains(search));
        }

        if (query.Status is { } status)
        {
            projects = projects.Where(p => p.Status == status);
        }

        if (query.OwnerId is { } ownerId)
        {
            projects = projects.Where(p => p.OwnerId == ownerId);
        }

        var sorted = query.SortBy?.ToLowerInvariant() switch
        {
            "key" => projects.OrderByDirection(p => p.Key, query.SortDirection),
            "createdat" => projects.OrderByDirection(p => p.CreatedAtUtc, query.SortDirection),
            "status" => projects.OrderByDirection(p => p.Status, query.SortDirection),
            _ => projects.OrderByDirection(p => p.Name, query.SortDirection),
        };


        return sorted
            .ThenBy(p => p.Id)
            .Select(p => new ProjectSummaryDto(
                p.Id,
                p.Key.Value,
                p.Name,
                p.Status,
                p.OwnerId,
                db.Users.Where(u => u.Id == p.OwnerId).Select(u => u.DisplayName).First(),
                db.WorkItems.Count(w => w.ProjectId == p.Id
                    && w.Status != WorkItemStatus.Completed
                    && w.Status != WorkItemStatus.Cancelled),
                p.CreatedAtUtc))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectLookupDto>> GetActiveLookupAsync(CancellationToken cancellationToken) =>
        await db.Projects.AsNoTracking()
            .Where(p => p.Status == ProjectStatus.Active)
            .OrderBy(p => p.Name)
            .Select(p => new ProjectLookupDto(p.Id, p.Key.Value, p.Name))
            .ToListAsync(cancellationToken);

    public void Add(Project project) => db.Projects.Add(project);

    private static ProjectKey? TryParseKey(string value)
    {
        try
        {
            return ProjectKey.Create(value);
        }
        catch (DomainException)
        {
            return null;
        }
    }
}
