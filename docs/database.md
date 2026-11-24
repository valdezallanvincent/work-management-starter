# Database

SQL Server through EF Core 10. The schema is defined with Fluent API configurations in
`src/Starter.Infrastructure/Persistence/Configurations` and evolved with migrations.

## Schema

```mermaid
erDiagram
    Users ||--o{ Projects : owns
    Users ||--o{ RefreshTokens : has
    Users ||--o{ Notifications : receives
    Projects ||--o{ WorkItems : contains
    Users ||--o{ WorkItems : "reports / is assigned"
    WorkItems ||--o{ WorkItemComments : has
    Users ||--o{ WorkItemComments : writes

    Users { guid Id PK; string Email UK; string Role; bool IsActive; int FailedLoginCount }
    Projects { guid Id PK; string Key UK; string Status; guid OwnerId FK; rowversion RowVersion }
    WorkItems { guid Id PK; guid ProjectId FK; int Status; int Priority; guid AssigneeId FK; date DueDate; rowversion RowVersion }
    WorkItemComments { guid Id PK; guid WorkItemId FK; guid AuthorId FK; string Body }
    RefreshTokens { guid Id PK; string TokenHash UK; guid FamilyId; datetimeoffset RevokedAtUtc }
    Notifications { guid Id PK; guid RecipientId FK; string Message; datetimeoffset ReadAtUtc }
    OutboxMessages { guid Id PK; string Type; string Payload; int AttemptCount; datetimeoffset ProcessedAtUtc }
    ProcessedMessages { guid MessageId PK; string Consumer PK }
```

Auditable tables also carry `CreatedAtUtc`, `CreatedById`, `ModifiedAtUtc` and `ModifiedById`.

## Decisions

**Identifiers.** Keys are `Guid.CreateVersion7()` values generated in the domain, not by the
database. Entities have their identity before they are saved (useful for events and logging),
and version 7 GUIDs are time-ordered, which avoids the page splits that random GUIDs cause in a
clustered index.

**References by id, not navigation properties.** Aggregates point to each other with plain ids
(`WorkItem.ProjectId`, `WorkItem.AssigneeId`). Foreign keys are still declared in the
configurations, so the database enforces integrity. Reads that need names use explicit joins
in projections. This keeps aggregates small, makes it impossible to lazy-load half the database
by accident, and leaves no `Include` chains to maintain.

**Enums.** Stored as strings by default (readable, safe to reorder). `WorkItem.Status` and
`WorkItem.Priority` are stored as integers because their order matters: sorting by priority as
text would put Medium above High. This was caught by an end-to-end check and is now covered by
an integration test.

**Delete behaviour.** Cascades only where the child has no meaning without the parent (comments
of a work item, a user's tokens and notifications). Everything that would destroy business
history is `Restrict`; projects are archived, not deleted.

**Concurrency.** `Projects` and `WorkItems` have a `rowversion` column configured with
`IsRowVersion()`. Clients receive it as a base64 `rowVersion` and send it back with every change.
Two checks protect an update:

1. The service compares the client's version with the loaded row and fails fast with `409` if
   they differ (the client was looking at stale data).
2. EF Core adds `WHERE RowVersion = @original` to the `UPDATE`. If another request committed
   between our read and our write, zero rows are affected, EF throws
   `DbUpdateConcurrencyException`, and the unit of work turns that into `409`.

The second check is what closes the race; the first just gives a clearer error earlier. Both are
covered by integration tests. `RefreshTokens` uses `RevokedAtUtc` as a concurrency token instead,
because revocation is the only update a token row ever receives.

## Indexes

| Index | Serves |
| --- | --- |
| `Users(Email)` unique | Login lookup and uniqueness |
| `Projects(Key)` unique | Key uniqueness and search by key |
| `Projects(Status, Name)` | Default project list (active projects by name) |
| `WorkItems(ProjectId, Status)` | Project work lists and per-project counts by status |
| `WorkItems(AssigneeId, Status)` | "Assigned to me" and dashboard counts |
| `WorkItems(CompletedAtUtc)` filtered `IS NOT NULL` | "Completed in the last 7 days" |
| `WorkItemComments(WorkItemId, CreatedAtUtc)` | Comment thread in order |
| `RefreshTokens(TokenHash)` unique, `(UserId, FamilyId)` | Refresh lookup and family revocation |
| `OutboxMessages(NextAttemptAtUtc, OccurredAtUtc)` filtered to pending rows | Dispatcher polling; stays small as history grows |

Every foreign key column is indexed (SQL Server does not do this automatically).

## Query practices

- **Read paths project to DTOs** with `Select` and use `AsNoTracking`. Only the columns the
  screen needs are read, and the change tracker is not involved.
- **No N+1 queries.** Lists join users and projects in a single statement; per-project counts are
  correlated subqueries in the same SQL. Project details issue exactly two queries (the row with
  names, then one grouped count by status).
- **Write paths load tracked aggregates** by id, call domain methods and save once.
- **Pagination** runs a `COUNT` and an `OFFSET/FETCH` page query. Every sort ends with `Id` so
  pages are deterministic when values tie. The page size is capped at 100 by validation.
- **Sorting is allow-listed** through a `switch` on known column names; user input never builds
  an expression or SQL fragment.
- **Search** uses `LIKE '%term%'` (EF `Contains`), parameterised. This cannot use an index; it is
  acceptable for this data volume, and full-text search is the documented next step.
- **Set-based updates** (`ExecuteUpdateAsync`) are used where loading entities would be wasteful,
  such as cancelling every open item of an archived project. They bypass the change tracker and
  interceptors, so they set audit columns explicitly.

## Transactions

A single `SaveChangesAsync` is already atomic, and most workflows need nothing more. Explicit
transactions appear only where a workflow needs more than one round trip to be atomic, or a
stronger isolation level:

| Workflow | Isolation | Why |
| --- | --- | --- |
| Archive project | Read committed | Update the project row first (its exclusive lock blocks concurrent work item creation), count active items, then set-based cancel of open items. Any rule failure rolls the whole thing back. |
| Create work item | Repeatable read | The project row is read and its shared lock held until commit, so an archive that starts concurrently must wait, and cancels the new item. Without it, an item could be created in a project archived a moment earlier. |
| Outbox batch | Read committed + `UPDLOCK, READPAST` | Claims rows so concurrent dispatchers skip each other's batches |

`EnableRetryOnFailure` is on, so explicit transactions go through
`Database.CreateExecutionStrategy()`. The whole unit is replayed after a transient failure such
as a deadlock victim (error 1205); the unit of work clears the change tracker before a retry so
the operation reloads fresh data.

## Migrations in practice

- Development and Docker Compose apply migrations on startup (`Database:ApplyMigrationsOnStartup`).
  EF Core 10 takes a database lock while migrating, so concurrent starts are safe, but production
  should still migrate from the deployment pipeline, before new instances start.
- CI fails if the model has changes without a migration (`dotnet ef migrations has-pending-model-changes`).
- Demo data is seeded only when the environment is Development and the database has no users.
