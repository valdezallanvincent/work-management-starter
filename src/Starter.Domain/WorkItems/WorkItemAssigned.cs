using Starter.Domain.Common;

namespace Starter.Domain.WorkItems;

public sealed record WorkItemAssigned(
    Guid WorkItemId,
    Guid ProjectId,
    string Title,
    Guid AssigneeId,
    Guid? AssignedById,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
