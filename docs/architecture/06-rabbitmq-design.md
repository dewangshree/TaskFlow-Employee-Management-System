# RabbitMQ Design

**Events** (`Domain/Events`, routing keys in `Application/Events/IntegrationEventCatalog.cs`): `task.assigned`, `task.status_changed`, `leave.requested`, `leave.reviewed`.

**Transactional outbox** (`AppDbContext.SaveChangesAsync`): aggregates queue events as factories. Inside one transaction, the context saves the aggregates (so identity ids exist), serialises the events to `OutboxMessages` (with the request's correlation id), saves again and commits. The API never talks to RabbitMQ, so a broker outage cannot fail a business request.

**Dispatcher** (`OutboxDispatcher`, run by `OutboxPublisherService` every 2s): `SELECT … FOR UPDATE SKIP LOCKED LIMIT 50`, then publishes with publisher confirms and `mandatory=true` before setting `ProcessedAt`. Failures back off exponentially (capped at 5 min); rows are parked after 20 attempts and surfaced by the `outbox` health check. Processed rows are purged after 7 days.

**Topology** (`RabbitMqTopology`): topic exchange `taskflow.events` feeds the durable queue `taskflow.notifications`. Its dead-letter target is `taskflow.notifications.dlq`. Retry queues `.retry.1/2/3` have TTLs of 5s/30s/120s and dead-letter back into the main queue (one queue per delay avoids head-of-line blocking).

**Consumer** (`NotificationConsumerService`): manual acks, prefetch 10.
- Success → ack.
- Transient failure → confirmed publish of a copy to the next retry queue with `x-retry-count`, then ack. After 3 retries the copy goes to the DLQ with `x-last-error`.
- Poison (unknown type, bad JSON, missing id) → `BasicReject(requeue:false)` → DLQ.

**Idempotency** (`NotificationMessageProcessor`): the `ProcessedMessages` check, notification inserts and the inbox insert run in one transaction. A unique-violation race is treated as a duplicate. The unique index `(UserId, SourceEventId)` is a second guard. Delivery is at-least-once, and the effect happens exactly once.

**Correlation:** the correlation id flows HTTP → outbox → AMQP `CorrelationId` → worker log context.
