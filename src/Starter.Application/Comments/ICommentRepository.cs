using Starter.Domain.WorkItems;

namespace Starter.Application.Comments;

public interface ICommentRepository
{
    Task<WorkItemComment?> GetByIdAsync(Guid workItemId, Guid commentId, CancellationToken cancellationToken);

    Task<CommentDto?> FindDtoAsync(Guid commentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CommentDto>> ListForWorkItemAsync(Guid workItemId, CancellationToken cancellationToken);

    void Add(WorkItemComment comment);

    void Remove(WorkItemComment comment);
}
