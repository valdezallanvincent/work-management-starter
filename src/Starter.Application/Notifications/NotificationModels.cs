namespace Starter.Application.Notifications;

public sealed record NotificationDto(
    Guid Id,
    string Message,
    Guid? WorkItemId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadAtUtc);

public sealed record NotificationListDto(IReadOnlyList<NotificationDto> Items, int UnreadCount);
