using Microsoft.EntityFrameworkCore;
using Starter.Application.Comments;
using Starter.Domain.WorkItems;

namespace Starter.Infrastructure.Persistence.Repositories;

internal sealed class CommentRepository(StarterDbContext db) : ICommentRepository
{
    public Task<WorkItemComment?> GetByIdAsync(Guid workItemId, Guid commentId, CancellationToken cancellationToken) =>
        db.WorkItemComments.FirstOrDefaultAsync(c => c.Id == commentId && c.WorkItemId == workItemId, cancellationToken);

    public Task<CommentDto?> FindDtoAsync(Guid commentId, CancellationToken cancellationToken) =>
        Project(db.WorkItemComments.Where(c => c.Id == commentId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CommentDto>> ListForWorkItemAsync(Guid workItemId, CancellationToken cancellationToken) =>
        await Project(db.WorkItemComments.Where(c => c.WorkItemId == workItemId).OrderBy(c => c.CreatedAtUtc))
            .ToListAsync(cancellationToken);

    public void Add(WorkItemComment comment) => db.WorkItemComments.Add(comment);

    public void Remove(WorkItemComment comment) => db.WorkItemComments.Remove(comment);

    private IQueryable<CommentDto> Project(IQueryable<WorkItemComment> comments) =>
        from c in comments.AsNoTracking()
        join author in db.Users on c.AuthorId equals author.Id
        select new CommentDto(c.Id, c.WorkItemId, c.AuthorId, author.DisplayName, c.Body, c.IsEdited, c.CreatedAtUtc, c.ModifiedAtUtc);
}
