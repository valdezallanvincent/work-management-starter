using Starter.Application.Common.Security;
using Starter.Application.Tests.Fakes;
using Starter.Domain.Users;

namespace Starter.Application.Tests;

public sealed class AccessRulesTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Reporter = Guid.NewGuid();
    private static readonly Guid Assignee = Guid.NewGuid();

    private static TestCurrentUser As(Guid id, UserRole role = UserRole.Member) => new() { UserIdOrDefault = id, Role = role };

    [Fact]
    public void Administrators_can_manage_any_project() =>
        Assert.True(AccessRules.CanManageProject(As(Guid.NewGuid(), UserRole.Admin), Owner));

    [Fact]
    public void The_manager_role_alone_does_not_grant_management_of_someone_elses_project() =>
        Assert.False(AccessRules.CanManageProject(As(Guid.NewGuid(), UserRole.Manager), Owner));

    [Fact]
    public void Reporter_and_assignee_can_edit_but_only_the_assignee_can_change_status()
    {
        Assert.True(AccessRules.CanEditWorkItem(As(Reporter), Owner, Reporter, Assignee));
        Assert.True(AccessRules.CanEditWorkItem(As(Assignee), Owner, Reporter, Assignee));
        Assert.False(AccessRules.CanChangeStatus(As(Reporter), Owner, Assignee));
        Assert.True(AccessRules.CanChangeStatus(As(Assignee), Owner, Assignee));
    }

    [Fact]
    public void Unrelated_members_can_only_self_assign_unassigned_work()
    {
        var member = Guid.NewGuid();
        Assert.True(AccessRules.CanAssign(As(member), Owner, currentAssigneeId: null, newAssigneeId: member));
        Assert.False(AccessRules.CanAssign(As(member), Owner, currentAssigneeId: null, newAssigneeId: Assignee));
        Assert.False(AccessRules.CanAssign(As(member), Owner, currentAssigneeId: Assignee, newAssigneeId: member));
    }
}
