using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Starter.Domain.Users;
using Starter.Domain.WorkItems;

namespace Starter.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class ProjectsApiTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Managers_create_projects_and_get_a_location_header()
    {
        using var client = await factory.SignInAsync(await factory.CreateUserAsync(UserRole.Manager));
        var key = ApiFactory.UniqueKey();

        var response = await client.PostAsJsonAsync("/api/v1/projects", new { key = key.ToLowerInvariant(), name = "Depot upgrade" }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var project = await response.ReadAsync<ProjectResponse>();
        Assert.Equal(key, project.Key);
        Assert.True(project.CanManage);
        Assert.EndsWith($"/api/v1/projects/{project.Id}", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Members_cannot_create_projects()
    {
        using var client = await factory.SignInAsync(await factory.CreateUserAsync(UserRole.Member));

        var response = await client.PostAsJsonAsync("/api/v1/projects", new { key = ApiFactory.UniqueKey(), name = "Not allowed" }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_project_keys_are_rejected_with_conflict()
    {
        using var client = await factory.SignInAsync(await factory.CreateUserAsync(UserRole.Manager));
        var project = await client.CreateProjectAsync();

        var response = await client.PostAsJsonAsync("/api/v1/projects", new { key = project.Key, name = "Copy" }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("project.key_taken", (await response.ReadAsync<ProblemResponse>()).Code);
    }

    [Fact]
    public async Task Invalid_input_returns_validation_problem_details()
    {
        using var client = await factory.SignInAsync(await factory.CreateUserAsync(UserRole.Manager));

        var response = await client.PostAsJsonAsync("/api/v1/projects", new { key = "1", name = "" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadAsync<ProblemResponse>();
        Assert.NotNull(problem.Errors);
        Assert.Contains("Key", problem.Errors.Keys);
        Assert.Contains("Name", problem.Errors.Keys);
    }

    [Fact]
    public async Task Only_the_owner_or_an_administrator_can_change_a_project()
    {
        using var owner = await factory.SignInAsync(await factory.CreateUserAsync(UserRole.Manager));
        using var otherManager = await factory.SignInAsync(await factory.CreateUserAsync(UserRole.Manager));
        var project = await owner.CreateProjectAsync();

        var response = await otherManager.PutAsJsonAsync(
            $"/api/v1/projects/{project.Id}", new { name = "Taken over", ownerId = project.OwnerId, rowVersion = project.RowVersion }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Archiving_cancels_open_items_and_blocks_new_work_in_one_transaction()
    {
        using var client = await factory.SignInAsync(await factory.CreateUserAsync(UserRole.Manager));
        var project = await client.CreateProjectAsync();
        await client.CreateWorkItemAsync(project.Id, "First");
        await client.CreateWorkItemAsync(project.Id, "Second");
        project = await (await client.GetAsync($"/api/v1/projects/{project.Id}", Ct)).ReadAsync<ProjectResponse>();

        var archived = await (await client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/archive", new { project.RowVersion }, Ct)).ReadAsync<ProjectResponse>();

        Assert.Equal("Archived", archived.Status);
        Assert.Equal(2, archived.WorkItemCountsByStatus["Cancelled"]);
        var create = await client.PostAsJsonAsync("/api/v1/work-items", new { projectId = project.Id, title = "Late", priority = "Low" }, Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, create.StatusCode);
        Assert.Equal("project.archived", (await create.ReadAsync<ProblemResponse>()).Code);
    }

    [Fact]
    public async Task Archiving_with_work_in_progress_is_rejected_and_rolls_back_completely()
    {
        var manager = await factory.CreateUserAsync(UserRole.Manager);
        using var client = await factory.SignInAsync(manager);
        var project = await client.CreateProjectAsync();
        var open = await client.CreateWorkItemAsync(project.Id, "Still open", manager.Id);
        var active = await client.CreateWorkItemAsync(project.Id, "Being worked on", manager.Id);
        await client.PutAsJsonAsync($"/api/v1/work-items/{active.Id}/status", new { status = "InProgress", active.RowVersion }, Ct);
        project = await (await client.GetAsync($"/api/v1/projects/{project.Id}", Ct)).ReadAsync<ProjectResponse>();

        var response = await client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/archive", new { project.RowVersion }, Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("project.has_active_work", (await response.ReadAsync<ProblemResponse>()).Code);
        var state = await factory.WithDbAsync(async db => new
        {
            Project = await db.Projects.AsNoTracking().SingleAsync(p => p.Id == project.Id, Ct),
            OpenStatus = await db.WorkItems.Where(w => w.Id == open.Id).Select(w => w.Status).SingleAsync(Ct),
        });
        Assert.False(state.Project.IsArchived);
        Assert.Equal(WorkItemStatus.Open, state.OpenStatus);
    }

    [Fact]
    public async Task Project_lookup_cache_is_invalidated_when_projects_change()
    {
        using var client = await factory.SignInAsync(await factory.CreateUserAsync(UserRole.Manager));
        await client.GetAsync("/api/v1/projects/lookup", Ct); // warm the cache

        var project = await client.CreateProjectAsync();
        var afterCreate = await (await client.GetAsync("/api/v1/projects/lookup", Ct)).ReadAsync<List<LookupResponse>>();
        Assert.Contains(afterCreate, p => p.Id == project.Id);

        await client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/archive", new { project.RowVersion }, Ct);
        var afterArchive = await (await client.GetAsync("/api/v1/projects/lookup", Ct)).ReadAsync<List<LookupResponse>>();
        Assert.DoesNotContain(afterArchive, p => p.Id == project.Id);
    }
}
