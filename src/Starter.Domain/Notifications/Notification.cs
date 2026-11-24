using Starter.Domain.Common;

namespace Starter.Domain.Notifications;

/// <summary>
/// An in-app notification created by background processing, never directly by an API request.
/// </summary>
public sealed class Notification : Entity
{
    public const int MessageMaxLength = 500;

    private Notification(Guid id, Guid recipientId, string message, Guid? workItemId, DateTimeOffset createdAtUtc)
        : base(id)
    {
        RecipientId = recipientId;
        Message = message;
        WorkItemId = workItemId;
        CreatedAtUtc = createdAtUtc;
    }

    private Notification()
    {
    }

    public Guid RecipientId { get; private set; }

    public string Message { get; private set; } = null!;

    public Guid? WorkItemId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    public static Notification Create(Guid recipientId, string message, Guid? workItemId, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), recipientId, Guard.RequiredText(message, MessageMaxLength, "notification.message_invalid", "Message"), workItemId, now);

    public void MarkRead(DateTimeOffset now) => ReadAtUtc ??= now;
}
