using Microsoft.AspNetCore.Mvc;
using Starter.Application.Comments;
using Starter.Application.Common.Paging;
using Starter.Application.WorkItems;

namespace Starter.Api.Controllers;

[Route("api/v{version:apiVersion}/work-items")]
public sealed class WorkItemsController(IWorkItemService workItemService, ICommentService commentService) : ApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<WorkItemSummaryDto>> Search([FromQuery] WorkItemQuery query, CancellationToken cancellationToken) =>
        workItemService.SearchAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<WorkItemDetailsDto> Get(Guid id, CancellationToken cancellationToken) =>
        workItemService.GetAsync(id, cancellationToken);

    [HttpPost]
    [ProducesResponseType<WorkItemDetailsDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<WorkItemDetailsDto>> Create(CreateWorkItemRequest request, CancellationToken cancellationToken)
    {
        var workItem = await workItemService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = workItem.Id, version = "1" }, workItem);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public Task<WorkItemDetailsDto> Update(Guid id, UpdateWorkItemRequest request, CancellationToken cancellationToken) =>
        workItemService.UpdateAsync(id, request, cancellationToken);

    [HttpPut("{id:guid}/priority")]
    public Task<WorkItemDetailsDto> ChangePriority(Guid id, ChangeWorkItemPriorityRequest request, CancellationToken cancellationToken) =>
        workItemService.ChangePriorityAsync(id, request, cancellationToken);

    [HttpPut("{id:guid}/assignee")]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<WorkItemDetailsDto> Assign(Guid id, AssignWorkItemRequest request, CancellationToken cancellationToken) =>
        workItemService.AssignAsync(id, request, cancellationToken);

    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public Task<WorkItemDetailsDto> ChangeStatus(Guid id, ChangeWorkItemStatusRequest request, CancellationToken cancellationToken) =>
        workItemService.ChangeStatusAsync(id, request, cancellationToken);

    [HttpGet("{id:guid}/comments")]
    public Task<IReadOnlyList<CommentDto>> ListComments(Guid id, CancellationToken cancellationToken) =>
        commentService.ListAsync(id, cancellationToken);

    [HttpPost("{id:guid}/comments")]
    [ProducesResponseType<CommentDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CommentDto>> AddComment(Guid id, CommentRequest request, CancellationToken cancellationToken)
    {
        var comment = await commentService.AddAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(ListComments), new { id, version = "1" }, comment);
    }

    [HttpPut("{id:guid}/comments/{commentId:guid}")]
    public Task<CommentDto> EditComment(Guid id, Guid commentId, CommentRequest request, CancellationToken cancellationToken) =>
        commentService.EditAsync(id, commentId, request, cancellationToken);

    [HttpDelete("{id:guid}/comments/{commentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteComment(Guid id, Guid commentId, CancellationToken cancellationToken)
    {
        await commentService.DeleteAsync(id, commentId, cancellationToken);
        return NoContent();
    }
}
