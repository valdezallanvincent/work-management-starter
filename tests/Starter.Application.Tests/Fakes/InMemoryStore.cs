using System.Data;
using Starter.Application.Authentication;
using Starter.Application.Common.Caching;
using Starter.Application.Common.Exceptions;
using Starter.Application.Common.Paging;
using Starter.Application.Common.Persistence;
using Starter.Application.Common.Security;
using Starter.Application.Dashboard;
using Starter.Application.Notifications;
using Starter.Application.Projects;
using Starter.Application.Users;
using Starter.Application.WorkItems;
using Starter.Domain.Notifications;
using Starter.Domain.Projects;
using Starter.Domain.Users;
using Starter.Domain.WorkItems;

namespace Starter.Application.Tests.Fakes;

/// <summary>
/// Hand-written in-memory implementations of the persistence ports. They keep real domain objects,
/// so service tests exercise real business rules; only the database is replaced. Query methods that
/// these tests do not need are left unimplemented on purpose.
/// </summary>
internal sealed class InMemoryStore :
    IUserRepository, IProjectRepository, IWorkItemRepository, IRefreshTokenRepository, INotificationRepository, IUnitOfWork
{
    public List<User> Users { get; } = [];
    public List<Project> Projects { get; } = [];
    public List<WorkItem> WorkItems { get; } = [];
    public List<RefreshToken> RefreshTokens { get; } = [];
    public List<Notification> Notifications { get; } = [];
    public int SaveCount { get; private set; }

    /// <summary>Simulates the database rejecting a save, for example because of a concurrent update.</summary>
    public Exception? FailNextSaveWith { get; set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (FailNextSaveWith is { } failure)
        {
            FailNextSaveWith = null;
            throw failure;
        }

        SaveCount++;
        return Task.CompletedTask;
    }

    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken) =>
        operation(cancellationToken);

    Task<User?> IUserRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.FromResult(Users.FirstOrDefault(u => u.Email == normalizedEmail));

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.FromResult(Users.Any(u => u.Email == normalizedEmail));

    public Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Users.Any(u => u.Id == id && u.IsActive));

    public Task<UserDto?> FindDtoAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Users.Where(u => u.Id == id).Select(u => new UserDto(u.Id, u.Email, u.DisplayName, u.Role, u.IsActive, u.LastLoginAtUtc, u.CreatedAtUtc)).FirstOrDefault());

    public Task<PagedResult<UserDto>> SearchAsync(UserQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<UserLookupDto>> GetActiveLookupAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<UserLookupDto>>(Users.Where(u => u.IsActive).Select(u => new UserLookupDto(u.Id, u.DisplayName)).ToList());

    public void Add(User user) => Users.Add(user);

    Task<Project?> IProjectRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Projects.FirstOrDefault(p => p.Id == id));

    public Task<bool> KeyExistsAsync(ProjectKey key, CancellationToken cancellationToken) => Task.FromResult(Projects.Any(p => p.Key == key));

    public Task<ProjectDetailsDto?> FindDetailsAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Projects.Where(p => p.Id == id).Select(p => new ProjectDetailsDto(
            p.Id, p.Key.Value, p.Name, p.Description, p.Status, p.OwnerId, "Owner", p.ArchivedAtUtc, p.CreatedAtUtc, null, null, null,
            new Dictionary<string, int>(), RowVersion.Encode(p.RowVersion))).FirstOrDefault());

    public Task<PagedResult<ProjectSummaryDto>> SearchAsync(ProjectQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

    Task<IReadOnlyList<ProjectLookupDto>> IProjectRepository.GetActiveLookupAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProjectLookupDto>>(Projects.Where(p => !p.IsArchived).Select(p => new ProjectLookupDto(p.Id, p.Key.Value, p.Name)).ToList());

    public void Add(Project project) => Projects.Add(StampRowVersion(project));

    Task<WorkItem?> IWorkItemRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(WorkItems.FirstOrDefault(w => w.Id == id));

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(WorkItems.Any(w => w.Id == id));

    Task<WorkItemDetailsDto?> IWorkItemRepository.FindDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        var w = WorkItems.FirstOrDefault(x => x.Id == id);
        if (w is null)
        {
            return Task.FromResult<WorkItemDetailsDto?>(null);
        }

        var p = Projects.First(x => x.Id == w.ProjectId);
        return Task.FromResult<WorkItemDetailsDto?>(new WorkItemDetailsDto(
            w.Id, p.Id, p.Key.Value, p.Name, p.OwnerId, p.IsArchived, w.Title, w.Description, w.Status, w.Priority, w.ReporterId, "Reporter",
            w.AssigneeId, null, w.DueDate, w.CompletedAtUtc, w.CreatedAtUtc, w.ModifiedAtUtc, null,
            WorkItemStatusTransitions.From(w.Status), RowVersion.Encode(w.RowVersion)));
    }

    public Task<PagedResult<WorkItemSummaryDto>> SearchAsync(WorkItemQuery query, DateOnly today, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<int> CountInProgressAsync(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult(WorkItems.Count(w => w.ProjectId == projectId && w.Status is WorkItemStatus.InProgress or WorkItemStatus.InReview));

    public Task<int> CancelOpenWorkItemsAsync(Guid projectId, DateTimeOffset now, Guid? modifiedById, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<DashboardDto> GetDashboardAsync(Guid userId, DateOnly today, CancellationToken cancellationToken) => throw new NotSupportedException();

    public void Add(WorkItem workItem) => WorkItems.Add(StampRowVersion(workItem));

    /// <summary>SQL Server assigns rowversion values on insert; the fake does the same so version checks behave realistically.</summary>
    public static T StampRowVersion<T>(T entity)
        where T : class
    {
        typeof(T).GetProperty("RowVersion")!.SetValue(entity, BitConverter.GetBytes(Random.Shared.NextInt64()));
        return entity;
    }

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) => Task.FromResult(RefreshTokens.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, string reason, CancellationToken cancellationToken)
    {
        RefreshTokens.Where(t => t.FamilyId == familyId).ToList().ForEach(t => t.Revoke(now, reason));
        return Task.CompletedTask;
    }

    public Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, string reason, CancellationToken cancellationToken)
    {
        RefreshTokens.Where(t => t.UserId == userId).ToList().ForEach(t => t.Revoke(now, reason));
        return Task.CompletedTask;
    }

    public void Add(RefreshToken token) => RefreshTokens.Add(token);

    public Task<NotificationListDto> GetRecentAsync(Guid recipientId, int take, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Notification?> GetAsync(Guid recipientId, Guid notificationId, CancellationToken cancellationToken) =>
        Task.FromResult(Notifications.FirstOrDefault(n => n.Id == notificationId && n.RecipientId == recipientId));

    public Task<int> MarkAllReadAsync(Guid recipientId, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();

    public void Add(Notification notification) => Notifications.Add(notification);
}

internal sealed class TestCurrentUser : ICurrentUser
{
    public Guid? UserIdOrDefault { get; set; }

    public UserRole Role { get; set; } = UserRole.Member;

    public bool IsAuthenticated => UserIdOrDefault is not null;

    public Guid UserId => UserIdOrDefault ?? throw new InvalidOperationException("No user.");

    public void SignInAs(User user)
    {
        UserIdOrDefault = user.Id;
        Role = user.Role;
    }
}

/// <summary>Deterministic, readable stand-in for the PBKDF2 hasher.</summary>
internal sealed class PlainTextPasswordHasher : IPasswordHasher
{
    public string Hash(User user, string password) => "hashed:" + password;

    public PasswordVerification Verify(User user, string password) =>
        user.PasswordHash == "hashed:" + password ? PasswordVerification.Success : PasswordVerification.Failed;
}

internal sealed class FakeAccessTokenIssuer(TimeProvider time) : IAccessTokenIssuer
{
    public AccessToken Issue(User user) => new($"access-token-for-{user.Id}", time.GetUtcNow().AddMinutes(15));
}

internal sealed class PassThroughCache : IAppCache
{
    public List<string> Removed { get; } = [];

    public Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? expiration, CancellationToken cancellationToken) =>
        factory(cancellationToken);

    public Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        Removed.Add(key);
        return Task.CompletedTask;
    }
}

internal static class ConcurrencyFailure
{
    public static Exception Create() => new ConcurrencyConflictException("changed by someone else");
}
