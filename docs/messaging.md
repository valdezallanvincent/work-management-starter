# Messaging

## The workflow

When a work item is assigned, the assignee gets an in-app notification. That notification is
produced asynchronously:

- The assignment request should not wait for, or fail because of, notification delivery.
- A notification failure must never roll back the assignment.
- The same event is the natural hook for email, Teams or webhook notifications later, without
  touching the assignment code.

## Flow

```mermaid
sequenceDiagram
    participant S as WorkItemService
    participant DB as SQL Server
    participant P as OutboxProcessor
    participant T as Transport
    participant C as MessageConsumer
    participant H as WorkItemAssignedHandler

    S->>DB: BEGIN; UPDATE WorkItems; INSERT OutboxMessages; COMMIT
    loop every PollingInterval
        P->>DB: SELECT TOP n ... WITH (UPDLOCK, READPAST) pending rows
        P->>T: SendAsync(message)
        alt InProcess
            T->>C: ConsumeAsync
        else AzureServiceBus
            T->>T: send to queue (MessageId = outbox id)
            Note over T,C: ServiceBusConsumerService receives (peek-lock)
            T->>C: ConsumeAsync
        end
        C->>DB: already processed? (ProcessedMessages)
        C->>H: HandleAsync (stages a Notification)
        C->>DB: BEGIN; INSERT Notification; INSERT ProcessedMessage; COMMIT
        P->>DB: mark outbox row processed / record failure
    end
```

## The pieces

**Domain event.** `WorkItem.AssignTo` raises `WorkItemAssigned`. The domain does not know about
messaging.

**Transactional outbox** (`DomainEventOutboxInterceptor`). During `SaveChanges`, events on tracked
entities are serialised into `OutboxMessages` rows in the same transaction as the business
change. Either both are committed or neither is. This removes the dual-write problem: there is
no moment where the assignment is saved but the message is lost, or a message is sent for an
assignment that rolled back. Messages carry a versioned wire name (`work-item-assigned.v1`) and
the request's trace id as the correlation id.

**Dispatcher** (`OutboxProcessor`, a hosted service). Polls for pending rows whose
`NextAttemptAtUtc` has passed, claims them with `UPDLOCK, READPAST` so multiple API instances
do not process the same batch, and hands each one to the configured transport.

**Idempotent consumer** (`MessageConsumer`). Shared by both transports:

1. If `(MessageId, Consumer)` is already in `ProcessedMessages`, skip.
2. Run the handler. Handlers only stage changes on the scoped `DbContext`.
3. Insert the `ProcessedMessages` row and save, so the side effect and the record that it
   happened commit together.
4. If two deliveries of the same message race past step 1, the primary key on
   `ProcessedMessages` makes one of them fail with a unique violation; its transaction (including
   its notification) rolls back and it is treated as a duplicate.

At-least-once delivery plus this consumer gives an effectively-once side effect. The integration
tests deliver the same message twice in sequence and four times concurrently and assert exactly one
notification.

## Transports

| | `InProcess` (default) | `AzureServiceBus` |
| --- | --- | --- |
| Infrastructure | none beyond SQL Server | Service Bus namespace (Standard tier or the local emulator) |
| Where handlers run | the API process that dispatches | `ServiceBusConsumerService`, which could move to a separate worker |
| Durability | SQL Server: the outbox table is the queue | outbox until sent, then the broker |
| Retries | outbox `AttemptCount` with exponential backoff | broker redelivery (abandon) up to the queue's `MaxDeliveryCount` |
| Dead letters | `DeadLetteredAtUtc` set on the outbox row | the queue's dead-letter subqueue, with reason and description |
| Duplicates | idempotent consumer | broker duplicate detection on `MessageId`, plus the idempotent consumer |

### Reliability guarantees and limitations

- **Nothing in this repository holds messages only in memory.** The local transport reads from
  the SQL outbox, so a crash loses nothing that was committed. There is no in-memory queue whose
  contents vanish on restart.
- **Delivery is at least once in both modes.** A crash after a handler commits but before the
  outbox row is marked processed causes a redelivery. Duplicate detection on Service Bus only
  covers its configured window (5 minutes in the emulator config); the idempotent consumer covers
  the rest.
- **Ordering is not guaranteed** across retries or with concurrent consumers. The notification
  handler does not depend on order. Workflows that do would need sessions on Service Bus or a
  sequence check in the handler.
- **InProcess shares the API's resources.** Slow handlers compete with requests. Moving handlers to
  a separate worker process is the reason to switch to Service Bus.
- **Poison messages** (unknown type, unreadable payload) are dead-lettered immediately in both
  modes. Other failures are retried: up to `Messaging:MaxAttempts` with backoff from
  `Messaging:RetryBaseDelay` (5s, 10s, 20s, ... capped at 64x) in InProcess mode, and up to
  `MaxDeliveryCount` in Service Bus mode.
- **Dead-lettered messages need a person.** Nothing replays them automatically. In InProcess
  mode, clearing `DeadLetteredAtUtc` and resetting `NextAttemptAtUtc` requeues a row; in Service
  Bus mode, use Service Bus Explorer or a resubmit script.
- **History grows.** Processed outbox rows and `ProcessedMessages` are kept for diagnostics and
  are not purged. A scheduled cleanup (for example, delete rows older than 30 days) is needed for
  long-running systems. The inbox records must be kept at least as long as duplicates can arrive.

## Running with Service Bus locally

The [Azure Service Bus emulator](https://learn.microsoft.com/azure/service-bus-messaging/overview-emulator)
runs in Docker and needs no Azure subscription. The queue is defined in
`docker/servicebus/Config.json` (`MaxDeliveryCount` 5, duplicate detection on).

In `.env`:

```
MESSAGING_TRANSPORT=AzureServiceBus
SERVICEBUS_CONNECTION_STRING=Endpoint=sb://servicebus;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
```

Then:

```bash
docker compose --profile servicebus up --build
```

The emulator's connection string uses a fixed, publicly documented development key; it is not a
secret. A real namespace's connection string belongs in Key Vault or user secrets, and in Azure
a managed identity is preferable to a connection string.

During development the Service Bus path was checked against the emulator: an assignment
produced a notification through the queue, and a message with an unknown type was moved to the
dead-letter subqueue with reason `PoisonMessage`. That check was manual; the automated tests
cover the InProcess path, which shares the consumer pipeline.

## Adding a new message

1. Define the event in the domain and raise it from the entity method that causes it.
2. Register a wire name: `registry.Register<MyEvent>("my-event.v1")` in `DependencyInjection.cs`.
   Saving an unregistered event fails loudly rather than losing it.
3. Implement `IMessageHandler<MyEvent>` in the Application layer and register it as scoped.
   Stage changes only; do not call `SaveChanges` in the handler.
4. Make the handler safe to run twice anyway when it calls external systems (emails, HTTP).
   The inbox makes database side effects exactly-once, but it cannot un-send an email if the
   final commit fails.
