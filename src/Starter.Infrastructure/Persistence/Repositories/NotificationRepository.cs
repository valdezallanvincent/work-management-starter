using Microsoft.EntityFrameworkCore;
using Starter.Application.Notifications;
using Starter.Domain.Notifications;

namespace Starter.Infrastructure.Persistence.Repositories;

internal sealed class NotificationRepository(StarterDbContext db) : INotificationRepository
{
    public async Task<NotificationListDto> GetRecentAsync(Guid recipientId, int take, CancellationToken cancellationToken)
    {
        var mine = db.Notifications.AsNoTracking().Where(n => n.RecipientId == recipientId);

        var items = await mine
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(take)
            .Select(n => new NotificationDto(n.Id, n.Message, n.WorkItemId, n.CreatedAtUtc, n.ReadAtUtc))
            .ToListAsync(cancellationToken);

        var unread = await mine.CountAsync(n => n.ReadAtUtc == null, cancellationToken);
        return new NotificationListDto(items, unread);
    }

    public Task<Notification?> GetAsync(Guid recipientId, Guid notificationId, CancellationToken cancellationToken) =>
        db.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientId == recipientId, cancellationToken);

    public Task<int> MarkAllReadAsync(Guid recipientId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.Notifications
            .Where(n => n.RecipientId == recipientId && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAtUtc, now), cancellationToken);

    public void Add(Notification notification) => db.Notifications.Add(notification);
}
