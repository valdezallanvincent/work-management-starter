# 0004. Build a modular monolith, not microservices

Status: accepted

## Context

The system has a handful of closely related features (projects, work items, comments,
notifications) that share data and rules, and it targets a small team. Some work, such as
notifications, benefits from running asynchronously.

## Decision

Deploy one API process with one database. Keep features in separate folders within each layer,
and let them communicate across asynchronous boundaries through domain events and the outbox
where that brings a real benefit (notifications today).

## Alternatives considered

- **Microservices** (for example separate project, work item and notification services). Each
  could scale and deploy independently. In exchange: distributed transactions or sagas for things
  that are now a single `SaveChanges` (archiving a project and cancelling its work), network
  failures between services, duplicated reference data, separate pipelines, and distributed
  tracing to understand a single request. None of the forces that justify this (independent
  teams, very different scaling profiles, independent release cadence) apply here.
- **Unstructured monolith.** Simplest, but the boundaries that make a later split possible tend
  to erode.

## Consequences

- Strong consistency where it matters: an assignment and its outbox message, or an archive and
  its cancellations, commit atomically.
- One deployable artifact, one set of logs, simple local development.
- Scaling is horizontal for the whole API. Notification processing can be moved to its own
  worker process (switch the transport to Service Bus and host `ServiceBusConsumerService`
  separately) without changing handler code.
- Feature boundaries are conventions inside shared projects, not enforced by the compiler.
  Architecture tests (for example with NetArchTest) could enforce them if the codebase grows.

## When to revisit

When separate teams need to release independently, or one feature's load or availability needs
differ sharply from the others. The outbox events are the seam along which a service would be
extracted.
