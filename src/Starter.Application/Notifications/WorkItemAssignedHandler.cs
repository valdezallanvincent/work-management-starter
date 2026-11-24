using Microsoft.Extensions.Logging;
using Starter.Application.Common.Messaging;
using Starter.Application.Users;
using Starter.Domain.Notifications;
using Starter.Domain.WorkItems;

namespace Starter.Application.Notifications;

/// <summary>
/// Notifies the assignee. Runs asynchronously from the outbox so the assignment request does not
/// wait on notification delivery, and a notification failure never rolls back an assignment.
/// </summary>
internal sealed class WorkItemAssignedHandler(
    INotificationRepository notifications,
    IUserRepository users,
    TimeProvider timeProvider,
    ILogger<WorkItemAssignedHandler> logger) : IMessageHandler<WorkItemAssigned>
{
    public async Task HandleAsync(WorkItemAssigned message, MessageContext context, CancellationToken cancellationToken)
    {
        if (message.AssigneeId == message.AssignedById)
        {
            logger.LogDebug("Skipping notification for self-assignment of work item {WorkItemId}", message.WorkItemId);
            return;
        }

        var assignedBy = message.AssignedById is { } id ? await users.FindDtoAsync(id, cancellationToken) : null;
        var text = assignedBy is null
            ? $"You were assigned \"{Truncate(message.Title)}\"."
            : $"{assignedBy.DisplayName} assigned you \"{Truncate(message.Title)}\".";

        notifications.Add(Notification.Create(message.AssigneeId, text, message.WorkItemId, timeProvider.GetUtcNow()));
        logger.LogInformation(
            "Notification staged for assignee {AssigneeId} of work item {WorkItemId} (message {MessageId})",
            message.AssigneeId, message.WorkItemId, context.MessageId);
    }

    private static string Truncate(string title) => title.Length <= 120 ? title : string.Concat(title.AsSpan(0, 117), "...");
}
