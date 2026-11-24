using Starter.Domain.Common;

namespace Starter.Domain.Projects;

public sealed class Project : AuditableEntity
{
    public const int NameMaxLength = 120;
    public const int DescriptionMaxLength = 2000;

    private Project(Guid id, ProjectKey key, string name, string? description, Guid ownerId)
        : base(id)
    {
        Key = key;
        Name = name;
        Description = description;
        OwnerId = ownerId;
        Status = ProjectStatus.Active;
    }

    private Project()
    {
    }

    public ProjectKey Key { get; private set; }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public Guid OwnerId { get; private set; }

    public ProjectStatus Status { get; private set; }

    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool IsArchived => Status == ProjectStatus.Archived;

    public static Project Create(ProjectKey key, string name, string? description, Guid ownerId) =>
        new(
            Guid.CreateVersion7(),
            key,
            Guard.RequiredText(name, NameMaxLength, "project.name_invalid", "Project name"),
            Guard.OptionalText(description, DescriptionMaxLength, "project.description_invalid", "Description"),
            ownerId);

    public void UpdateDetails(string name, string? description)
    {
        EnsureNotArchived();
        Name = Guard.RequiredText(name, NameMaxLength, "project.name_invalid", "Project name");
        Description = Guard.OptionalText(description, DescriptionMaxLength, "project.description_invalid", "Description");
    }

    public void TransferOwnership(Guid newOwnerId)
    {
        EnsureNotArchived();
        OwnerId = newOwnerId;
    }

    public void Archive(DateTimeOffset now)
    {
        if (IsArchived)
        {
            throw new DomainException("project.already_archived", "The project is already archived.");
        }

        Status = ProjectStatus.Archived;
        ArchivedAtUtc = now;
    }

    public void Restore()
    {
        if (!IsArchived)
        {
            throw new DomainException("project.not_archived", "Only archived projects can be restored.");
        }

        Status = ProjectStatus.Active;
        ArchivedAtUtc = null;
    }

    public void EnsureCanAcceptWorkItems()
    {
        if (IsArchived)
        {
            throw new DomainException("project.archived", "Archived projects cannot accept new or changed work items.");
        }
    }

    private void EnsureNotArchived()
    {
        if (IsArchived)
        {
            throw new DomainException("project.archived", "Archived projects are read-only. Restore the project first.");
        }
    }
}
