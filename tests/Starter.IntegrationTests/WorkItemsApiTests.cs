using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Starter.Application.Common.Exceptions;
using Starter.Application.Common.Persistence;
using Starter.Domain.Users;
using Starter.Domain.WorkItems;
using Starter.Infrastructure.Persistence;

namespace Starter.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class WorkItemsApiTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Work_items_are_persisted_with_audit_information()
    {
        var manager = await factory.CreateUserAsync(UserRole.Manager);
        using var client = await factory.SignInAsync(manager);
        var project = await client.CreateProjectAsync();

        var created = await client.CreateWorkItemAsync(project.Id, "Audit me");

        var row = await factory.WithDbAsync(db => db.WorkItems.AsNoTracking().SingleAsync(w => w.Id == created.Id, Ct));
        Assert.Equal("Audit me", row.Title);
        Assert.Equal(manager.Id, row.CreatedById);
        Assert.Equal(manager.Id, row.ReporterId);
        Assert.True(row.CreatedAtUtc > DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.NotEmpty(row.RowVersion);
    }

    [Fact]
    public async Task Invalid_status_transitions_return_422_with_a_stable_code()
    {
        var manager = await factory.CreateUserAsync(UserRole.Manager);
        using var client = await factory.SignInAsync(manager);
        var item = await client.CreateWorkItemAsync((await client.CreateProjectAsync()).Id, assigneeId: manager.Id);

        var response = await client.PutAsJsonAsync($"/api/v1/work-items/{item.Id}/status", new { status = "Completed", item.RowVersion }, Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("work_item.invalid_status_transition", (await response.ReadAsync<ProblemResponse>()).Code);
    }

    [Fact]
    public async Task A_second_update_based_on_the_same_version_gets_409()
    {
        using var client = await factory.SignInAsync(await factory.CreateUserAsync(UserRole.Manager));
        var item = await client.CreateWorkItemAsync((await client.CreateProjectAsync()).Id);

        var first = await client.PutAsJsonAsync($"/api/v1/work-items/{item.Id}/priority", new { priority = "High", item.RowVersion }, Ct);
        var second = await client.PutAsJsonAsync($"/api/v1/work-items/{item.Id}/priority", new { priority = "Low", item.RowVersion }, Ct);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("concurrency_conflict", (await second.ReadAsync<ProblemResponse>()).Code);
        var stored = await factory.WithDbAsync(db => db.WorkItems.Where(w => w.Id == item.Id).Select(w => w.Priority).SingleAsync(Ct));
        Assert.Equal(WorkItemPriority.High, stored);
    }

    [Fact]
    public async Task The_database_rejects_a_write_based_on_a_stale_row_even_when_the_early_check_passed()
    {
        using var client = await factory.SignInAsync(await factory.CreateUserAsync(UserRole.Manager));
        var item = await client.CreateWorkItemAsync((await client.CreateProjectAsync()).Id);

        // Two requests read the same row before either one writes: the race the early version check cannot see.
        await using var first = factory.Services.CreateAsyncScope();
        await using var second = factory.Services.CreateAsyncScope();
        var firstItem = await first.ServiceProvider.GetRequiredService<StarterDbContext>().WorkItems.SingleAsync(w => w.Id == item.Id, Ct);
        var secondItem = await second.ServiceProvider.GetRequiredService<StarterDbContext>().WorkItems.SingleAsync(w => w.Id == item.Id, Ct);

        firstItem.ChangePriority(WorkItemPriority.Critical);
        await first.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);

        secondItem.ChangePriority(WorkItemPriority.Low);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => second.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Completed_items_cannot_be_edited_until_reopened()
    {
        var manager = await factory.CreateUserAsync(UserRole.Manager);
        using var client = await factory.SignInAsync(manager);
        var item = await client.CreateWorkItemAsync((await client.CreateProjectAsync()).Id, assigneeId: manager.Id);
        foreach (var status in new[] { "InProgress", "InReview", "Completed" })
        {
            item = await (await client.PutAsJsonAsync($"/api/v1/work-items/{item.Id}/status", new { status, item.RowVersion }, Ct)).ReadAsync<WorkItemResponse>();
        }

        var edit = await client.PutAsJsonAsync($"/api/v1/work-items/{item.Id}", new { title = "Changed", priority = "Low", item.RowVersion }, Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, edit.StatusCode);
        Assert.Equal("work_item.closed", (await edit.ReadAsync<ProblemResponse>()).Code);

        item = await (await client.PutAsJsonAsync($"/api/v1/work-items/{item.Id}/status", new { status = "Open", item.RowVersion }, Ct)).ReadAsync<WorkItemResponse>();
        var retry = await client.PutAsJsonAsync($"/api/v1/work-items/{item.Id}", new { title = "Changed", priority = "Low", item.RowVersion }, Ct);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }

    [Fact]
    public async Task Members_cannot_assign_other_people_through_the_api()
    {
        using var owner = await factory.SignInAsync(await factory.CreateUserAsync(UserRole.Manager));
        var member = await factory.CreateUserAsync(UserRole.Member);
        var colleague = await factory.CreateUserAsync(UserRole.Member);
        using var memberClient = await factory.SignInAsync(member);
        var item = await owner.CreateWorkItemAsync((await owner.CreateProjectAsync()).Id);

        var assignOther = await memberClient.PutAsJsonAsync($"/api/v1/work-items/{item.Id}/assignee", new { assigneeId = colleague.Id, item.RowVersion }, Ct);
        var assignSelf = await memberClient.PutAsJsonAsync($"/api/v1/work-items/{item.Id}/assignee", new { assigneeId = member.Id, item.RowVersion }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, assignOther.StatusCode);
        Assert.Equal(HttpStatusCode.OK, assignSelf.StatusCode);
    }

    [Fact]
    public async Task Search_filters_sorts_by_priority_order_and_paginates()
    {
        using var client = await factory.SignInAsync(await factory.CreateUserAsync(UserRole.Manager));
        var project = await client.CreateProjectAsync();
        foreach (var (title, priority) in new[] { ("alpha", "Low"), ("bravo", "Critical"), ("charlie", "Medium"), ("delta", "High") })
        {
            await client.PostAsJsonAsync("/api/v1/work-items", new { projectId = project.Id, title, priority }, Ct);
        }

        var page1 = await (await client.GetAsync($"/api/v1/work-items?projectId={project.Id}&sortBy=priority&sortDirection=Descending&pageSize=3", Ct))
            .ReadAsync<PagedResponse<WorkItemResponse>>();
        var page2 = await (await client.GetAsync($"/api/v1/work-items?projectId={project.Id}&sortBy=priority&sortDirection=Descending&pageSize=3&page=2", Ct))
            .ReadAsync<PagedResponse<WorkItemResponse>>();

        Assert.Equal(4, page1.TotalCount);
        Assert.Equal(["Critical", "High", "Medium"], page1.Items.Select(i => i.Priority));
        Assert.Equal("Low", Assert.Single(page2.Items).Priority);

        var search = await (await client.GetAsync($"/api/v1/work-items?projectId={project.Id}&search=arl", Ct)).ReadAsync<PagedResponse<WorkItemResponse>>();
        Assert.Equal("charlie", Assert.Single(search.Items).Title);
    }

    [Fact]
    public async Task Page_size_is_capped()
    {
        using var client = await factory.SignInAsync(await factory.CreateUserAsync(UserRole.Member));

        var response = await client.GetAsync("/api/v1/work-items?pageSize=5000", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
