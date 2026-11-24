using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Starter.Application.Common.Messaging;
using Starter.Application.Notifications;
using Starter.Application.Tests.Fakes;
using Starter.Domain.Users;
using Starter.Domain.WorkItems;

namespace Starter.Application.Tests;

public sealed class NotificationHandlerTests
{
    private readonly InMemoryStore _store = new();
    private readonly WorkItemAssignedHandler _handler;

    public NotificationHandlerTests() =>
        _handler = new WorkItemAssignedHandler(_store, _store, new FakeTimeProvider(), NullLogger<WorkItemAssignedHandler>.Instance);

    private static MessageContext Context => new(Guid.NewGuid(), "trace-1", 1);

    [Fact]
    public async Task Assignee_is_notified_with_the_name_of_whoever_assigned_the_work()
    {
        var manager = User.Create("dana@example.test", "Dana Reyes", UserRole.Manager, "x");
        _store.Users.Add(manager);
        var assignee = Guid.NewGuid();

        await _handler.HandleAsync(
            new WorkItemAssigned(Guid.NewGuid(), Guid.NewGuid(), "Pilot scanners", assignee, manager.Id, DateTimeOffset.UtcNow),
            Context,
            TestContext.Current.CancellationToken);

        var notification = Assert.Single(_store.Notifications);
        Assert.Equal(assignee, notification.RecipientId);
        Assert.Equal("Dana Reyes assigned you \"Pilot scanners\".", notification.Message);
    }

    [Fact]
    public async Task Self_assignment_does_not_notify()
    {
        var userId = Guid.NewGuid();

        await _handler.HandleAsync(
            new WorkItemAssigned(Guid.NewGuid(), Guid.NewGuid(), "Pilot scanners", userId, userId, DateTimeOffset.UtcNow),
            Context,
            TestContext.Current.CancellationToken);

        Assert.Empty(_store.Notifications);
    }
}
