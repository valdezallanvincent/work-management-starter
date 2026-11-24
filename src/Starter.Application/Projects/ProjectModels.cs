using System.ComponentModel.DataAnnotations;
using Starter.Application.Common.Paging;
using Starter.Domain.Projects;

namespace Starter.Application.Projects;

public sealed record ProjectSummaryDto(
    Guid Id,
    string Key,
    string Name,
    ProjectStatus Status,
    Guid OwnerId,
    string OwnerName,
    int OpenWorkItemCount,
    DateTimeOffset CreatedAtUtc);

public sealed record ProjectDetailsDto(
    Guid Id,
    string Key,
    string Name,
    string? Description,
    ProjectStatus Status,
    Guid OwnerId,
    string OwnerName,
    DateTimeOffset? ArchivedAtUtc,
    DateTimeOffset CreatedAtUtc,
    string? CreatedByName,
    DateTimeOffset? ModifiedAtUtc,
    string? ModifiedByName,
    IReadOnlyDictionary<string, int> WorkItemCountsByStatus,
    string RowVersion)
{
    /// <summary>Whether the current user may edit, archive or restore the project; set by the service.</summary>
    public bool CanManage { get; init; }
}

public sealed record ProjectLookupDto(Guid Id, string Key, string Name);

public sealed record ProjectQuery : PageQuery
{
    public ProjectStatus? Status { get; init; }

    public Guid? OwnerId { get; init; }
}

public sealed record CreateProjectRequest(
    [Required, StringLength(ProjectKey.MaxLength, MinimumLength = ProjectKey.MinLength), RegularExpression("^[A-Za-z][A-Za-z0-9]*$")] string Key,
    [Required, StringLength(Project.NameMaxLength, MinimumLength = 1)] string Name,
    [StringLength(Project.DescriptionMaxLength)] string? Description,
    Guid? OwnerId);

public sealed record UpdateProjectRequest(
    [Required, StringLength(Project.NameMaxLength, MinimumLength = 1)] string Name,
    [StringLength(Project.DescriptionMaxLength)] string? Description,
    Guid OwnerId,
    [Required] string RowVersion);

public sealed record ProjectVersionRequest([Required] string RowVersion);
