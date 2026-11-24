using Starter.Application.Common.Exceptions;
using Starter.Domain.Projects;
using Starter.Domain.Users;
using Starter.Domain.WorkItems;

namespace Starter.Application.Common.Security;

/// <summary>
/// Resource-level authorization. Role checks that do not depend on the resource are expressed
/// as ASP.NET Core policies on the controllers; rules that need the project owner or the work item's
/// people live here. The same rules produce the permission flags returned to the client, so the UI
/// never re-implements them.
/// </summary>
public static class AccessRules
{
    public static bool CanCreateProjects(ICurrentUser user) =>
        user.Role is UserRole.Admin or UserRole.Manager;

    public static bool CanManageProject(ICurrentUser user, Guid projectOwnerId) =>
        user.Role == UserRole.Admin || projectOwnerId == user.UserId;

    public static bool CanEditWorkItem(ICurrentUser user, Guid projectOwnerId, Guid reporterId, Guid? assigneeId) =>
        CanManageProject(user, projectOwnerId) || reporterId == user.UserId || assigneeId == user.UserId;

    public static bool CanChangeStatus(ICurrentUser user, Guid projectOwnerId, Guid? assigneeId) =>
        CanManageProject(user, projectOwnerId) || assigneeId == user.UserId;

    /// <summary>
    /// Project managers can assign anyone. Other users can only pick up unassigned work for themselves.
    /// </summary>
    public static bool CanAssign(ICurrentUser user, Guid projectOwnerId, Guid? currentAssigneeId, Guid? newAssigneeId) =>
        CanManageProject(user, projectOwnerId) || (currentAssigneeId is null && newAssigneeId == user.UserId);

    public static bool CanManageProject(ICurrentUser user, Project project) => CanManageProject(user, project.OwnerId);

    public static bool CanEditWorkItem(ICurrentUser user, Project project, WorkItem workItem) =>
        CanEditWorkItem(user, project.OwnerId, workItem.ReporterId, workItem.AssigneeId);

    public static bool CanChangeStatus(ICurrentUser user, Project project, WorkItem workItem) =>
        CanChangeStatus(user, project.OwnerId, workItem.AssigneeId);

    public static bool CanAssign(ICurrentUser user, Project project, WorkItem workItem, Guid? newAssigneeId) =>
        CanAssign(user, project.OwnerId, workItem.AssigneeId, newAssigneeId);

    public static void Demand(bool allowed, string message)
    {
        if (!allowed)
        {
            throw new ForbiddenAccessException(message);
        }
    }
}
