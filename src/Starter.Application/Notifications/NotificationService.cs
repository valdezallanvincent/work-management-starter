using Starter.Application.Common.Exceptions;
using Starter.Application.Common.Persistence;
using Starter.Application.Common.Security;

namespace Starter.Application.Notifications;

public interface INotificationService
{
    Task<NotificationListDto> GetRecentAsync(CancellationToken cancellationToken);

    Task MarkReadAsync(Guid notificationId, CancellationToken cancellationToken);

    Task MarkAllReadAsync(CancellationToken cancellationToken);
}

internal sealed class NotificationService(
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : INotificationService
{
    private const int RecentLimit = 20;

    public Task<NotificationListDto> GetRecentAsync(CancellationToken cancellationToken) =>
        notifications.GetRecentAsync(currentUser.UserId, RecentLimit, cancellationToken);

    public async Task MarkReadAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        // Scoped by recipient, so another user's notification id behaves as not found.
        var notification = await notifications.GetAsync(currentUser.UserId, notificationId, cancellationToken)
            ?? throw new NotFoundException("Notification", notificationId);

        notification.MarkRead(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task MarkAllReadAsync(CancellationToken cancellationToken) =>
        notifications.MarkAllReadAsync(currentUser.UserId, timeProvider.GetUtcNow(), cancellationToken);
}
