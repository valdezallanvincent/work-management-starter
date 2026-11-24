namespace Starter.Api.Security;

/// <summary>
/// Coarse, role-based gates applied at the controller. Rules that depend on the specific project
/// or work item (owner, assignee) are enforced in the application services.
/// </summary>
public static class AuthorizationPolicies
{
    public const string Administrators = nameof(Administrators);
    public const string ProjectCreators = nameof(ProjectCreators);

    public static void AddPolicies(Microsoft.AspNetCore.Authorization.AuthorizationBuilder builder)
    {
        builder
            .AddPolicy(Administrators, policy => policy.RequireRole("Admin"))
            .AddPolicy(ProjectCreators, policy => policy.RequireRole("Admin", "Manager"))
            // Every endpoint requires authentication unless it opts out with [AllowAnonymous].
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
    }
}
