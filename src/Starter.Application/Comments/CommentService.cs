using Starter.Application.Common.Exceptions;
using Starter.Application.Common.Persistence;
using Starter.Application.Common.Security;
using Starter.Application.Projects;
using Starter.Application.WorkItems;
using Starter.Domain.Users;
using Starter.Domain.WorkItems;

namespace Starter.Application.Comments;

internal sealed class CommentService(
    ICommentRepository comments,
    IWorkItemRepository workItems,
    IProjectRepository projects,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommentService
{
    public async Task<IReadOnlyList<CommentDto>> ListAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        if (!await workItems.ExistsAsync(workItemId, cancellationToken))
        {
            throw new NotFoundException("Work item", workItemId);
        }

        return await comments.ListForWorkItemAsync(workItemId, cancellationToken);
    }

    /// <summary>
    /// Comments stay open on completed or cancelled work so people can discuss why it was closed,
    /// but an archived project is read-only.
    /// </summary>
    public async Task<CommentDto> AddAsync(Guid workItemId, CommentRequest request, CancellationToken cancellationToken)
    {
        var workItem = await LoadWritableWorkItemAsync(workItemId, cancellationToken);

        var comment = WorkItemComment.Create(workItem, currentUser.UserId, request.Body);
        comments.Add(comment);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetDtoAsync(comment.Id, cancellationToken);
    }

    public async Task<CommentDto> EditAsync(Guid workItemId, Guid commentId, CommentRequest request, CancellationToken cancellationToken)
    {
        await LoadWritableWorkItemAsync(workItemId, cancellationToken);
        var comment = await comments.GetByIdAsync(workItemId, commentId, cancellationToken)
            ?? throw new NotFoundException("Comment", commentId);

        AccessRules.Demand(comment.AuthorId == currentUser.UserId, "Only the author can edit a comment.");
        comment.Edit(currentUser.UserId, request.Body);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetDtoAsync(comment.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid workItemId, Guid commentId, CancellationToken cancellationToken)
    {
        var comment = await comments.GetByIdAsync(workItemId, commentId, cancellationToken)
            ?? throw new NotFoundException("Comment", commentId);

        AccessRules.Demand(
            comment.AuthorId == currentUser.UserId || currentUser.Role == UserRole.Admin,
            "Only the author or an administrator can delete a comment.");

        comments.Remove(comment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<WorkItem> LoadWritableWorkItemAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        var workItem = await workItems.GetByIdAsync(workItemId, cancellationToken)
            ?? throw new NotFoundException("Work item", workItemId);
        var project = await projects.GetByIdAsync(workItem.ProjectId, cancellationToken)
            ?? throw new NotFoundException("Project", workItem.ProjectId);

        project.EnsureCanAcceptWorkItems();
        return workItem;
    }

    private async Task<CommentDto> GetDtoAsync(Guid commentId, CancellationToken cancellationToken) =>
        await comments.FindDtoAsync(commentId, cancellationToken) ?? throw new NotFoundException("Comment", commentId);
}
