using System.ComponentModel.DataAnnotations;
using Starter.Domain.WorkItems;

namespace Starter.Application.Comments;

public sealed record CommentDto(
    Guid Id,
    Guid WorkItemId,
    Guid AuthorId,
    string AuthorName,
    string Body,
    bool IsEdited,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ModifiedAtUtc);

public sealed record CommentRequest(
    [Required, StringLength(WorkItemComment.BodyMaxLength, MinimumLength = 1)] string Body);
