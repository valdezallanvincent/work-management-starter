using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Starter.Application.Common.Exceptions;
using Starter.Application.Common.Persistence;
using Starter.Application.Tests.Fakes;
using Starter.Application.WorkItems;
using Starter.Domain.Common;
using Starter.Domain.Projects;
using Starter.Domain.Users;
using Starter.Domain.WorkItems;

namespace Starter.Application.Tests;

public sealed class WorkItemServiceTests
{
    private readonly InMemoryStore _store = new();
    private readonly TestCurrentUser _currentUser = new();
    private readonly WorkItemService _service;
    private readonly User _owner = User.Create("owner@example.test", "Owner", UserRole.Manager, "x");
    private readonly User _member = User.Create("member@example.test", "Member", UserRole.Member, "x");
    private readonly User _otherMember = User.Create("other@example.test", "Other", UserRole.Member, "x");
    private readonly User _admin = User.Create("admin@example.test", "Admin", UserRole.Admin, "x");
    private readonly Project _project;

    public WorkItemServiceTests()
    {
        _store.Users.AddRange([_owner, _member, _otherMember, _admin]);
        _project = Project.Create(ProjectKey.Create("OPS"), "Operations", null, _owner.Id);
        _store.Add(_project);
        _service = new WorkItemService(_store, _store, _store, _store, _currentUser, new FakeTimeProvider(), NullLogger<WorkItemService>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private WorkItem AddItem(Guid? assigneeId = null)
    {
        var item = WorkItem.Create(_project, "Pick list export", null, WorkItemPriority.Medium, _owner.Id, null);
        if (assigneeId is not null)
        {
            item.AssignTo(assigneeId, _owner.Id, DateTimeOffset.UtcNow);
        }

        _store.Add(item);
        return item;
    }

    private static string Version(WorkItem item) => RowVersion.Encode(item.RowVersion);

    [Fact]
    public async Task Members_cannot_assign_work_to_other_people()
    {
        var item = AddItem();
        _currentUser.SignInAs(_member);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            _service.AssignAsync(item.Id, new AssignWorkItemRequest(_otherMember.Id, Version(item)), Ct));

        Assert.Null(item.AssigneeId);
        Assert.Equal(0, _store.SaveCount);
    }

    [Fact]
    public async Task Members_can_pick_up_unassigned_work_for_themselves()
    {
        var item = AddItem();
        _currentUser.SignInAs(_member);

        var result = await _service.AssignAsync(item.Id, new AssignWorkItemRequest(_member.Id, Version(item)), Ct);

        Assert.Equal(_member.Id, result.AssigneeId);
        Assert.IsType<WorkItemAssigned>(Assert.Single(item.DomainEvents));
    }

    [Fact]
    public async Task Members_cannot_take_work_already_assigned_to_someone_else()
    {
        var item = AddItem(_otherMember.Id);
        _currentUser.SignInAs(_member);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            _service.AssignAsync(item.Id, new AssignWorkItemRequest(_member.Id, Version(item)), Ct));
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Manager)]
    public async Task Project_owner_and_administrators_can_assign_anyone(UserRole role)
    {
        var item = AddItem();
        _currentUser.SignInAs(role == UserRole.Admin ? _admin : _owner);

        var result = await _service.AssignAsync(item.Id, new AssignWorkItemRequest(_otherMember.Id, Version(item)), Ct);

        Assert.Equal(_otherMember.Id, result.AssigneeId);
    }

    [Fact]
    public async Task Managers_who_do_not_own_the_project_have_member_rights_in_it()
    {
        var otherManager = User.Create("m2@example.test", "Manager Two", UserRole.Manager, "x");
        _store.Users.Add(otherManager);
        var item = AddItem();
        _currentUser.SignInAs(otherManager);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            _service.AssignAsync(item.Id, new AssignWorkItemRequest(_member.Id, Version(item)), Ct));
    }

    [Fact]
    public async Task Work_cannot_be_assigned_to_a_deactivated_user()
    {
        var item = AddItem();
        _otherMember.Deactivate();
        _currentUser.SignInAs(_owner);

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            _service.AssignAsync(item.Id, new AssignWorkItemRequest(_otherMember.Id, Version(item)), Ct));
        Assert.Equal("work_item.assignee_invalid", exception.Code);
    }

    [Fact]
    public async Task Only_the_assignee_or_project_owner_can_change_status()
    {
        var item = AddItem(_member.Id);
        _currentUser.SignInAs(_otherMember);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            _service.ChangeStatusAsync(item.Id, new ChangeWorkItemStatusRequest(WorkItemStatus.InProgress, Version(item)), Ct));

        _currentUser.SignInAs(_member);
        var result = await _service.ChangeStatusAsync(item.Id, new ChangeWorkItemStatusRequest(WorkItemStatus.InProgress, Version(item)), Ct);
        Assert.Equal(WorkItemStatus.InProgress, result.Status);
    }

    [Fact]
    public async Task Invalid_status_transitions_are_rejected_before_saving()
    {
        var item = AddItem(_member.Id);
        _currentUser.SignInAs(_owner);

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            _service.ChangeStatusAsync(item.Id, new ChangeWorkItemStatusRequest(WorkItemStatus.Completed, Version(item)), Ct));

        Assert.Equal("work_item.invalid_status_transition", exception.Code);
        Assert.Equal(0, _store.SaveCount);
    }

    [Fact]
    public async Task A_stale_version_is_reported_as_a_concurrency_conflict()
    {
        var item = AddItem();
        _currentUser.SignInAs(_owner);
        var staleVersion = Convert.ToBase64String([0, 0, 0, 0, 0, 0, 0, 9]);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            _service.ChangePriorityAsync(item.Id, new ChangeWorkItemPriorityRequest(WorkItemPriority.High, staleVersion), Ct));

        Assert.Equal(WorkItemPriority.Medium, item.Priority);
    }

    [Fact]
    public async Task Work_items_in_archived_projects_cannot_be_changed()
    {
        var item = AddItem();
        _project.Archive(DateTimeOffset.UtcNow);
        _currentUser.SignInAs(_owner);

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            _service.ChangePriorityAsync(item.Id, new ChangeWorkItemPriorityRequest(WorkItemPriority.High, Version(item)), Ct));

        Assert.Equal("project.archived", exception.Code);
    }

    [Fact]
    public async Task Permissions_returned_to_the_client_follow_the_same_rules()
    {
        var item = AddItem(_otherMember.Id);

        _currentUser.SignInAs(_member);
        var asMember = await _service.GetAsync(item.Id, Ct);
        Assert.Equal(new WorkItemPermissions(false, false, false, false), asMember.Permissions);

        _currentUser.SignInAs(_owner);
        var asOwner = await _service.GetAsync(item.Id, Ct);
        Assert.Equal(new WorkItemPermissions(true, true, true, true), asOwner.Permissions);
    }
}
