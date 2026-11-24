using Microsoft.AspNetCore.Mvc;
using Starter.Application.Notifications;

namespace Starter.Api.Controllers;

[Route("api/v{version:apiVersion}/notifications")]
public sealed class NotificationsController(INotificationService notificationService) : ApiControllerBase
{
    [HttpGet]
    public Task<NotificationListDto> Recent(CancellationToken cancellationToken) =>
        notificationService.GetRecentAsync(cancellationToken);

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        await notificationService.MarkReadAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await notificationService.MarkAllReadAsync(cancellationToken);
        return NoContent();
    }
}
