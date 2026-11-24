# 0001. Layer the backend with Clean Architecture

Status: accepted

## Context

The starter has real business rules (a status workflow, ownership-based permissions, archive
semantics) alongside infrastructure that is likely to change: the cache provider is configurable,
the message transport is swappable, and the database could move to Azure SQL. The code should make
it obvious where a rule lives, and the rules should be testable without a database or web server.

## Decision

Four projects with dependencies pointing inwards: Domain, Application, Infrastructure, Api.
The compiler enforces the direction through project references. Domain has no package
references at all. Application references only Microsoft.Extensions abstractions.

## Alternatives considered

- **Single project with folders.** Fastest to start and perfectly fine for small CRUD services.
  Nothing stops a controller from reaching into `DbContext`, though, and over time business rules
  tend to spread into controllers and queries.
- **Vertical slice architecture.** Organises by feature instead of layer, with each slice owning
  its endpoint, logic and data access. It works very well for request-centric applications
  with thin domains. Here several features share one domain model (a work item's rules matter to
  projects, comments and notifications), which fits a central domain layer better.
- **Hexagonal (ports and adapters).** Essentially the same dependency rule with different
  vocabulary. The Application interfaces in this repository are its ports.

## Consequences

- Domain rules are tested with plain unit tests (55 tests, under a second).
- Swapping Redis for memory, or Service Bus for the in-process dispatcher, touches only
  Infrastructure and configuration.
- More files and more indirection than a single project: a new feature touches up to four
  projects. Features are still grouped by folder inside each project to keep related code together.
- Application interfaces must not leak infrastructure types. Repositories return DTOs for read
  models, which is a pragmatic bend: read DTOs are defined in Application and projected by EF Core
  in Infrastructure, avoiding a second mapping step.

## When to revisit

If the domain stays thin (mostly CRUD), a single project or vertical slices would be less
ceremony. If parts of the system need independent deployment, the feature folders are the seams
to split along, not the layers.
