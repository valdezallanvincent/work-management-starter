# 0002. Use application services instead of a mediator pipeline

Status: accepted

## Context

Application logic needs a home between controllers and the domain. Common options in .NET are
service classes, a mediator with one handler per request (MediatR or similar), or one use-case
class per operation.

## Decision

One service interface and implementation per feature (`IWorkItemService`, `IProjectService`, and
so on). Controllers call service methods directly. Cross-cutting concerns use the framework's
own mechanisms: model validation through `[ApiController]`, errors through `IExceptionHandler`,
logging through `ILogger`, and transactions through `IUnitOfWork`.

## Alternatives considered

- **Mediator with handlers.** Gives every request the same shape and a pipeline for behaviours
  (validation, logging, transactions). The cost is indirection: "go to definition" from a
  controller lands on `Send`, not on the code that runs. The pipeline behaviours it is usually
  adopted for are already provided by ASP.NET Core here. MediatR is also commercially licensed
  from version 13, which matters for a template others will copy.
- **One class per use case.** Very focused classes, but many small files that share the same
  dependencies, and related operations (assign, change status, change priority) end up far apart.

## Consequences

- The workflow for an operation can be read top to bottom in one method.
- Services are easy to test directly; the tests construct them with in-memory fakes.
- Services can grow large. `WorkItemService` is the biggest at around 170 lines. If one passes a
  few hundred lines or mixes unrelated responsibilities, splitting it (for example a separate
  `WorkItemAssignmentService`) is the first step, before reaching for a pipeline.
- Cross-cutting behaviour that is not covered by ASP.NET Core (for example auditing every command
  to a separate store) would need to be added per service or via decorators.

## When to revisit

A mediator starts to pay off when there are many entry points other than HTTP (message
handlers, scheduled jobs, CLI) that all need the same pipeline behaviours, or when a team wants a
strict "one request, one handler" convention across a large codebase.
