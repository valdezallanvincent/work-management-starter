namespace Starter.Application.Comments;

public interface ICommentService
{
    Task<IReadOnlyList<CommentDto>> ListAsync(Guid workItemId, CancellationToken cancellationToken);

    Task<CommentDto> AddAsync(Guid workItemId, CommentRequest request, CancellationToken cancellationToken);

    Task<CommentDto> EditAsync(Guid workItemId, Guid commentId, CommentRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid workItemId, Guid commentId, CancellationToken cancellationToken);
}
