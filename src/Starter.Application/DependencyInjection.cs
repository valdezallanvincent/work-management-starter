using Microsoft.Extensions.DependencyInjection;
using Starter.Application.Authentication;
using Starter.Application.Comments;
using Starter.Application.Common.Messaging;
using Starter.Application.Dashboard;
using Starter.Application.Notifications;
using Starter.Application.Projects;
using Starter.Application.Users;
using Starter.Application.WorkItems;
using Starter.Domain.WorkItems;

namespace Starter.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Services are scoped because they depend on the scoped unit of work and current user.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IWorkItemService, WorkItemService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<INotificationService, NotificationService>();

        services.AddScoped<IMessageHandler<WorkItemAssigned>, WorkItemAssignedHandler>();

        return services;
    }
}
