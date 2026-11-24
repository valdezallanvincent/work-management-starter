using Starter.Domain.Common;
using Starter.Domain.WorkItems;

namespace Starter.Domain.Tests;

public sealed class WorkItemStatusTransitionTests
{
    private static readonly HashSet<(WorkItemStatus From, WorkItemStatus To)> Allowed =
    [
        (WorkItemStatus.Open, WorkItemStatus.InProgress),
        (WorkItemStatus.Open, WorkItemStatus.Cancelled),
        (WorkItemStatus.InProgress, WorkItemStatus.InReview),
        (WorkItemStatus.InProgress, WorkItemStatus.Open),
        (WorkItemStatus.InProgress, WorkItemStatus.Cancelled),
        (WorkItemStatus.InReview, WorkItemStatus.Completed),
        (WorkItemStatus.InReview, WorkItemStatus.InProgress),
        (WorkItemStatus.InReview, WorkItemStatus.Cancelled),
        (WorkItemStatus.Completed, WorkItemStatus.Open),
        (WorkItemStatus.Cancelled, WorkItemStatus.Open),
    ];

    public static TheoryData<WorkItemStatus, WorkItemStatus> AllDistinctPairs()
    {
        var data = new TheoryData<WorkItemStatus, WorkItemStatus>();
        foreach (var from in Enum.GetValues<WorkItemStatus>())
        {
            foreach (var to in Enum.GetValues<WorkItemStatus>().Where(to => to != from))
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllDistinctPairs))]
    public void Every_transition_matches_the_documented_workflow(WorkItemStatus from, WorkItemStatus to)
    {
        var item = TestData.ItemIn(from);
        var expectedAllowed = Allowed.Contains((from, to));

        var exception = Record.Exception(() => item.ChangeStatus(to, TestData.Now));

        if (expectedAllowed)
        {
            Assert.Null(exception);
            Assert.Equal(to, item.Status);
        }
        else
        {
            var domainException = Assert.IsType<DomainException>(exception);
            Assert.Equal("work_item.invalid_status_transition", domainException.Code);
            Assert.Equal(from, item.Status);
        }
    }

    [Fact]
    public void Starting_work_requires_an_assignee()
    {
        var item = TestData.OpenItem();

        var exception = Assert.Throws<DomainException>(() => item.ChangeStatus(WorkItemStatus.InProgress, TestData.Now));

        Assert.Equal("work_item.assignee_required", exception.Code);
    }

    [Fact]
    public void Completing_records_the_completion_time_and_reopening_clears_it()
    {
        var item = TestData.ItemIn(WorkItemStatus.InReview);

        item.ChangeStatus(WorkItemStatus.Completed, TestData.Now);
        Assert.Equal(TestData.Now, item.CompletedAtUtc);

        item.ChangeStatus(WorkItemStatus.Open, TestData.Now.AddDays(1));
        Assert.Null(item.CompletedAtUtc);
    }
}
