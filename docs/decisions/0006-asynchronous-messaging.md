# 0006. Deliver assignment notifications asynchronously through a transactional outbox

Status: accepted

## Context

Assigning a work item should notify the assignee. Doing it inline in the request means the
request waits for notification work, a notification failure surfaces as a failed assignment, and
every new channel (email, Teams) adds latency and failure modes to the assignment endpoint.
Publishing directly to a broker after `SaveChanges` creates a dual write: if the process stops
between the commit and the publish, the notification is lost; publishing before the commit can
announce an assignment that then rolls back.

## Decision

- The domain raises `WorkItemAssigned`. A `SaveChanges` interceptor writes it to an outbox table
  in the same transaction as the assignment.
- A hosted dispatcher sends outbox rows to a transport: in-process handlers by default, or an
  Azure Service Bus queue when configured.
- Consumers are idempotent: the handler's changes and a processed-message record commit together.

## How it improves this workflow

- The assignment response returns as soon as the database commits; notification work happens
  afterwards.
- Notification failures are retried with backoff and end in a dead-letter state instead of
  surfacing as API errors.
- New reactions to an assignment are new handlers; the assignment code does not change.
- With Service Bus, consumers can run in a separate process and scale independently.

## Alternatives considered

- **Synchronous call in the service.** Simplest, and acceptable when the side effect is cheap,
  local and must be visible immediately. Here it would couple assignment success to notification
  delivery.
- **Publish to the broker after commit, without an outbox.** Loses messages on crashes between
  commit and publish.
- **Change data capture** on the work item table. No application changes, but events become row
  diffs instead of named business events, and the infrastructure is heavier.
- **A full messaging framework** (MassTransit, NServiceBus, Wolverine). Mature outbox, retries,
  sagas and transport abstraction. Worth adopting as the number of message types and workflows
  grows; here the hand-written version is small enough to read and explain, and avoids
  framework licensing questions.

## Consequences

- Notifications are eventually consistent: typically visible within the 2-second polling
  interval, longer during retries.
- Delivery is at least once; every handler must tolerate duplicates.
- Two extra tables need a retention policy.
- Polling adds a small constant load on the database. At higher volumes, waking the dispatcher
  immediately after a commit (or using CDC) reduces both latency and polling.
