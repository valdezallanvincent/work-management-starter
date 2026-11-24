using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Api.Security;
using Starter.Application.Common.Paging;
using Starter.Application.Projects;

namespace Starter.Api.Controllers;

[Route("api/v{version:apiVersion}/projects")]
public sealed class ProjectsController(IProjectService projectService) : ApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<ProjectSummaryDto>> Search([FromQuery] ProjectQuery query, CancellationToken cancellationToken) =>
        projectService.SearchAsync(query, cancellationToken);

    /// <summary>Active projects for pickers. Cached; see docs/caching.md.</summary>
    [HttpGet("lookup")]
    public Task<IReadOnlyList<ProjectLookupDto>> Lookup(CancellationToken cancellationToken) =>
        projectService.GetLookupAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ProjectDetailsDto> Get(Guid id, CancellationToken cancellationToken) =>
        projectService.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.ProjectCreators)]
    [ProducesResponseType<ProjectDetailsDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProjectDetailsDto>> Create(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await projectService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = project.Id, version = "1" }, project);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ProjectDetailsDto> Update(Guid id, UpdateProjectRequest request, CancellationToken cancellationToken) =>
        projectService.UpdateAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public Task<ProjectDetailsDto> Archive(Guid id, ProjectVersionRequest request, CancellationToken cancellationToken) =>
        projectService.ArchiveAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/restore")]
    public Task<ProjectDetailsDto> Restore(Guid id, ProjectVersionRequest request, CancellationToken cancellationToken) =>
        projectService.RestoreAsync(id, request, cancellationToken);
}
