using Starter.Application.Common.Paging;
using Starter.Domain.Projects;

namespace Starter.Application.Projects;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> KeyExistsAsync(ProjectKey key, CancellationToken cancellationToken);

    Task<ProjectDetailsDto?> FindDetailsAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<ProjectSummaryDto>> SearchAsync(ProjectQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectLookupDto>> GetActiveLookupAsync(CancellationToken cancellationToken);

    void Add(Project project);
}
