using System.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Caching;
using Starter.Application.Common.Exceptions;
using Starter.Application.Common.Paging;
using Starter.Application.Common.Persistence;
using Starter.Application.Common.Security;
using Starter.Application.Users;
using Starter.Application.WorkItems;
using Starter.Domain.Common;
using Starter.Domain.Projects;

namespace Starter.Application.Projects;

internal sealed class ProjectService(
    IProjectRepository projects,
    IWorkItemRepository workItems,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IAppCache cache,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IOptions<CachingOptions> cachingOptions,
    ILogger<ProjectService> logger) : IProjectService
{
    public Task<PagedResult<ProjectSummaryDto>> SearchAsync(ProjectQuery query, CancellationToken cancellationToken) =>
        projects.SearchAsync(query, cancellationToken);

    public async Task<ProjectDetailsDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var details = await projects.FindDetailsAsync(id, cancellationToken) ?? throw new NotFoundException("Project", id);
        return details with { CanManage = AccessRules.CanManageProject(currentUser, details.OwnerId) };
    }

    public Task<IReadOnlyList<ProjectLookupDto>> GetLookupAsync(CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            CacheKeys.ProjectLookup,
            projects.GetActiveLookupAsync,
            cachingOptions.Value.LookupExpiration,
            cancellationToken);

    public async Task<ProjectDetailsDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        AccessRules.Demand(AccessRules.CanCreateProjects(currentUser), "Only managers and administrators can create projects.");

        var key = ProjectKey.Create(request.Key);
        if (await projects.KeyExistsAsync(key, cancellationToken))
        {
            throw new ConflictException("project.key_taken", $"Project key '{key}' is already in use.");
        }

        var ownerId = request.OwnerId ?? currentUser.UserId;
        await EnsureActiveUserAsync(ownerId, cancellationToken);

        var project = Project.Create(key, request.Name, request.Description, ownerId);
        projects.Add(project);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await InvalidateLookupAsync(cancellationToken);

        logger.LogInformation("Project {ProjectId} ({ProjectKey}) created", project.Id, project.Key);
        return await GetAsync(project.Id, cancellationToken);
    }

    public async Task<ProjectDetailsDto> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await LoadForManagementAsync(id, request.RowVersion, cancellationToken);

        if (project.OwnerId != request.OwnerId)
        {
            await EnsureActiveUserAsync(request.OwnerId, cancellationToken);
            project.TransferOwnership(request.OwnerId);
        }

        project.UpdateDetails(request.Name, request.Description);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await InvalidateLookupAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Archiving is all-or-nothing: the project is archived and its open work items are cancelled,
    /// or nothing changes. The project row is updated first so that its exclusive lock blocks work
    /// item creation (which reads the project under REPEATABLE READ) until this transaction ends.
    /// </summary>
    public async Task<ProjectDetailsDto> ArchiveAsync(Guid id, ProjectVersionRequest request, CancellationToken cancellationToken)
    {
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var project = await LoadForManagementAsync(id, request.RowVersion, ct);
                var now = timeProvider.GetUtcNow();

                project.Archive(now);
                await unitOfWork.SaveChangesAsync(ct);

                var activeCount = await workItems.CountInProgressAsync(id, ct);
                if (activeCount > 0)
                {
                    throw new DomainException(
                        "project.has_active_work",
                        $"The project has {activeCount} work item(s) in progress or in review. Complete or cancel them before archiving.");
                }

                var cancelled = await workItems.CancelOpenWorkItemsAsync(id, now, currentUser.UserIdOrDefault, ct);
                logger.LogInformation("Project {ProjectId} archived; {CancelledCount} open work items cancelled", id, cancelled);
            },
            IsolationLevel.ReadCommitted,
            cancellationToken);

        await InvalidateLookupAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<ProjectDetailsDto> RestoreAsync(Guid id, ProjectVersionRequest request, CancellationToken cancellationToken)
    {
        var project = await LoadForManagementAsync(id, request.RowVersion, cancellationToken);
        project.Restore();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await InvalidateLookupAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private async Task<Project> LoadForManagementAsync(Guid id, string rowVersion, CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Project", id);
        AccessRules.Demand(AccessRules.CanManageProject(currentUser, project), "Only the project owner or an administrator can change this project.");
        RowVersion.EnsureMatches(project.RowVersion, rowVersion, "project");
        return project;
    }

    private async Task EnsureActiveUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!await users.IsActiveAsync(userId, cancellationToken))
        {
            throw new DomainException("project.owner_invalid", "The project owner must be an active user.");
        }
    }

    private Task InvalidateLookupAsync(CancellationToken cancellationToken) =>
        cache.RemoveAsync(CacheKeys.ProjectLookup, cancellationToken);
}
