namespace Starter.Domain.Common;

/// <summary>
/// Audit values are assigned by the persistence layer when changes are saved,
/// so domain methods never need to be passed the current user or clock for auditing.
/// </summary>
public abstract class AuditableEntity : Entity
{
    protected AuditableEntity(Guid id) : base(id)
    {
    }

    protected AuditableEntity()
    {
    }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Guid? CreatedById { get; private set; }

    public DateTimeOffset? ModifiedAtUtc { get; private set; }

    public Guid? ModifiedById { get; private set; }
}
