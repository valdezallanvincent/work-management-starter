using Starter.Domain.Common;

namespace Starter.Domain.WorkItems;

/// <summary>
/// Comments are a separate aggregate from <see cref="WorkItem"/>: adding a comment should not
/// load every existing comment or conflict with a concurrent edit of the work item itself.
/// </summary>
public sealed class WorkItemComment : AuditableEntity
{
    public const int BodyMaxLength = 4000;

    private WorkItemComment(Guid id, Guid workItemId, Guid authorId, string body)
        : base(id)
    {
        WorkItemId = workItemId;
        AuthorId = authorId;
        Body = body;
    }

    private WorkItemComment()
    {
    }

    public Guid WorkItemId { get; private set; }

    public Guid AuthorId { get; private set; }

    public string Body { get; private set; } = null!;

    public bool IsEdited { get; private set; }

    public static WorkItemComment Create(WorkItem workItem, Guid authorId, string body)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        return new WorkItemComment(Guid.CreateVersion7(), workItem.Id, authorId, ValidateBody(body));
    }

    public void Edit(Guid editorId, string body)
    {
        if (editorId != AuthorId)
        {
            throw new DomainException("comment.not_author", "Only the author can edit a comment.");
        }

        Body = ValidateBody(body);
        IsEdited = true;
    }

    private static string ValidateBody(string body) =>
        Guard.RequiredText(body, BodyMaxLength, "comment.body_invalid", "Comment");
}
