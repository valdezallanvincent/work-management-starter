using Starter.Application.Common.Paging;

namespace Starter.Application.Projects;

public interface IProjectService
{
    Task<PagedResult<ProjectSummaryDto>> SearchAsync(ProjectQuery query, CancellationToken cancellationToken);

    Task<ProjectDetailsDto> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectLookupDto>> GetLookupAsync(CancellationToken cancellationToken);

    Task<ProjectDetailsDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken);

    Task<ProjectDetailsDto> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken cancellationToken);

    Task<ProjectDetailsDto> ArchiveAsync(Guid id, ProjectVersionRequest request, CancellationToken cancellationToken);

    Task<ProjectDetailsDto> RestoreAsync(Guid id, ProjectVersionRequest request, CancellationToken cancellationToken);
}
