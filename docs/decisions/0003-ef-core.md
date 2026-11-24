# 0003. Use Entity Framework Core for data access

Status: accepted

## Context

The application needs relational persistence with migrations, concurrency control, transactions
and efficient read queries, on SQL Server in development and Azure SQL in the cloud.

## Decision

EF Core 10 with the SQL Server provider, configured with the Fluent API in Infrastructure so the
domain classes carry no persistence attributes. Write paths load tracked aggregates; read paths
project to DTOs with `AsNoTracking`. Repositories are feature-specific and expose operations the
application needs (`SearchAsync`, `CountInProgressAsync`), not generic CRUD.

## Alternatives considered

- **Dapper.** Full control over SQL and very fast for reads. Change tracking, concurrency tokens,
  migrations and mapping of private setters would all be hand-written. A reasonable addition for
  specific heavy report queries; not a good default for a domain with many small updates.
- **Generic repository over EF Core** (`IRepository<T>` with `GetAll`, `Add`, `Update`). It
  duplicates what `DbSet<T>` already is, hides EF features behind a lowest common denominator,
  and usually leaks `IQueryable` anyway. Feature repositories with intention-revealing methods
  were chosen instead.
- **Exposing `DbContext` to the Application layer** (an `IApplicationDbContext` interface).
  Fewer types and full LINQ in services, but Application then depends on EF Core and query
  logic spreads across services. Acceptable in smaller projects.

## Consequences

- Migrations, row-version concurrency, interceptors (auditing, outbox) and execution strategies
  come from the framework.
- Query shape needs discipline: every list query is a projection, there are no `Include` chains,
  and navigation properties are avoided between aggregates so nothing loads by accident.
- Set-based operations (`ExecuteUpdateAsync`) bypass interceptors, so they set audit columns
  themselves. This is easy to forget and is noted where it is used.
- Repositories return Application DTOs from read methods. It keeps one projection step instead
  of two, at the cost of read repositories knowing about the shapes screens need.

## When to revisit

If reporting queries grow complex enough that LINQ obscures the intent or produces poor plans,
use Dapper or raw SQL for those specific reads while keeping EF Core for writes.
