using Starter.Domain.Notifications;

namespace Starter.Application.Notifications;

public interface INotificationRepository
{
    Task<NotificationListDto> GetRecentAsync(Guid recipientId, int take, CancellationToken cancellationToken);

    Task<Notification?> GetAsync(Guid recipientId, Guid notificationId, CancellationToken cancellationToken);

    Task<int> MarkAllReadAsync(Guid recipientId, DateTimeOffset now, CancellationToken cancellationToken);

    void Add(Notification notification);
}
