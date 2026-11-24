using Starter.Domain.Common;
using Starter.Domain.WorkItems;

namespace Starter.Domain.Tests;

public sealed class WorkItemTests
{
    [Theory]
    [InlineData(WorkItemStatus.Completed)]
    [InlineData(WorkItemStatus.Cancelled)]
    public void Closed_work_items_cannot_be_modified_until_reopened(WorkItemStatus closedStatus)
    {
        var item = TestData.ItemIn(closedStatus);

        Assert.Equal("work_item.closed", Assert.Throws<DomainException>(() => item.UpdateDetails("New title", null, null)).Code);
        Assert.Equal("work_item.closed", Assert.Throws<DomainException>(() => item.ChangePriority(WorkItemPriority.High)).Code);
        Assert.Equal("work_item.closed", Assert.Throws<DomainException>(() => item.AssignTo(Guid.NewGuid(), null, TestData.Now)).Code);

        item.ChangeStatus(WorkItemStatus.Open, TestData.Now);
        item.ChangePriority(WorkItemPriority.High);

        Assert.Equal(WorkItemPriority.High, item.Priority);
    }

    [Fact]
    public void Assigning_raises_an_event_only_when_the_assignee_changes()
    {
        var item = TestData.OpenItem();
        var assignee = Guid.NewGuid();

        item.AssignTo(assignee, TestData.OwnerId, TestData.Now);
        item.AssignTo(assignee, TestData.OwnerId, TestData.Now);

        var assigned = Assert.IsType<WorkItemAssigned>(Assert.Single(item.DomainEvents));
        Assert.Equal(assignee, assigned.AssigneeId);
        Assert.Equal(item.Id, assigned.WorkItemId);
        Assert.Equal(TestData.OwnerId, assigned.AssignedById);
    }

    [Fact]
    public void Unassigning_raises_no_event()
    {
        var item = TestData.OpenItem();
        item.AssignTo(Guid.NewGuid(), null, TestData.Now);
        item.ClearDomainEvents();

        item.AssignTo(null, null, TestData.Now);

        Assert.Null(item.AssigneeId);
        Assert.Empty(item.DomainEvents);
    }

    [Theory]
    [InlineData(WorkItemStatus.InProgress)]
    [InlineData(WorkItemStatus.InReview)]
    public void Active_work_cannot_be_left_without_an_assignee(WorkItemStatus status)
    {
        var item = TestData.ItemIn(status);

        var exception = Assert.Throws<DomainException>(() => item.AssignTo(null, null, TestData.Now));

        Assert.Equal("work_item.assignee_required", exception.Code);
        Assert.Equal(TestData.MemberId, item.AssigneeId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Title_is_required(string title)
    {
        var exception = Assert.Throws<DomainException>(() =>
            WorkItem.Create(TestData.ActiveProject(), title, null, WorkItemPriority.Low, TestData.OwnerId, null));

        Assert.Equal("work_item.title_invalid", exception.Code);
    }

    [Fact]
    public void Text_is_trimmed_and_blank_descriptions_become_null()
    {
        var item = WorkItem.Create(TestData.ActiveProject(), "  Update labels  ", "   ", WorkItemPriority.Low, TestData.OwnerId, null);

        Assert.Equal("Update labels", item.Title);
        Assert.Null(item.Description);
    }

    [Fact]
    public void Undefined_priority_values_are_rejected()
    {
        var item = TestData.OpenItem();

        Assert.Throws<DomainException>(() => item.ChangePriority((WorkItemPriority)42));
    }

    [Fact]
    public void Only_the_author_can_edit_a_comment()
    {
        var author = Guid.NewGuid();
        var comment = WorkItemComment.Create(TestData.OpenItem(), author, "First draft");

        Assert.Equal("comment.not_author", Assert.Throws<DomainException>(() => comment.Edit(Guid.NewGuid(), "Hijacked")).Code);

        comment.Edit(author, "Second draft");
        Assert.Equal("Second draft", comment.Body);
        Assert.True(comment.IsEdited);
    }
}
