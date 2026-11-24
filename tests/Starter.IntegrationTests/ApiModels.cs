using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Starter.IntegrationTests;

internal sealed record ProjectResponse(Guid Id, string Key, string Name, string Status, Guid OwnerId, string RowVersion, Dictionary<string, int> WorkItemCountsByStatus, bool CanManage);

internal sealed record WorkItemResponse(Guid Id, Guid ProjectId, string Title, string Status, string Priority, Guid? AssigneeId, string RowVersion, string[] AllowedTransitions);

internal sealed record ProblemResponse(int Status, string? Title, string? Detail, string? Code, Dictionary<string, string[]>? Errors);

internal sealed record PagedResponse<T>(List<T> Items, int Page, int PageSize, int TotalCount);

internal sealed record LookupResponse(Guid Id, string Key, string Name);

internal static class HttpExtensions
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Json);
        return value ?? throw new InvalidOperationException("Empty response body.");
    }

    public static async Task<ProjectResponse> CreateProjectAsync(this HttpClient client, string? key = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/projects", new { key = key ?? ApiFactory.UniqueKey(), name = "Depot upgrade", description = (string?)null });
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<ProjectResponse>();
    }

    public static async Task<WorkItemResponse> CreateWorkItemAsync(this HttpClient client, Guid projectId, string title = "Replace scanners", Guid? assigneeId = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/work-items", new { projectId, title, priority = "Medium", assigneeId });
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<WorkItemResponse>();
    }
}
