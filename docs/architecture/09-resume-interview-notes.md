# Resume & Interview Notes (implemented features only)

**Resume bullets**
- Rebuilt an ASP.NET Core MVC employee-management app as a React 19 + TypeScript SPA and a versioned .NET 10 REST API using Clean Architecture (Domain/Application/Infrastructure/API) plus a background worker.
- Implemented a transactional outbox and RabbitMQ event pipeline (topic exchange, publisher confirms, tiered delayed retries, dead-letter queue, idempotent inbox consumer) to deliver in-app notifications without dual-write inconsistency.
- Migrated SQLite to PostgreSQL with EF Core: composite, partial and pg_trgm GIN expression indexes, CHECK constraints, and xmin-based optimistic concurrency that prevents lost updates and duplicate leave approvals.
- Added Redis cache-aside for dashboards, lookups and notification counters, with user-scoped keys, TTL plus explicit invalidation, and circuit-breaker fallback to PostgreSQL.
- Hardened the API with ASP.NET Core rate limiting (token bucket, fixed and sliding window, 429 + Retry-After), Identity lockout, CSRF protection, ProblemDetails errors, Serilog structured logs with end-to-end correlation IDs, and health checks.
- Built server-side paginated, sortable, debounced-search tables, optimistic updates with rollback on 409, route-level code splitting and memoised rows using TanStack Query, React Hook Form and Zod.

**Likely questions**
- *Why an outbox?* Saving a row and publishing a message are two systems; without a shared transaction one can succeed alone. The outbox makes the publish part of the DB commit.
- *How is duplicate processing prevented?* At-least-once delivery plus an inbox table with PK (MessageId, Consumer), written in the same transaction as the notifications, and a unique (UserId, SourceEventId) index as backstop.
- *Two admins approve at once?* Both send the xmin `version` they loaded. The second UPDATE matches 0 rows, EF throws `DbUpdateConcurrencyException`, and the API returns 409. Sequential double-approval is blocked by the domain rule "only Pending can be reviewed".
- *Redis down?* The circuit breaker skips Redis for 30s, reads go to PostgreSQL, readiness reports Degraded, and TTLs bound staleness.
- *Why are events created as factories?* Identity ids only exist after INSERT, so events are materialised after the first save, inside the same transaction.

**Challenges solved:** id-less events before insert; the CSRF token must be reissued after login because it is bound to the identity; Postgres `timestamptz` rejecting non-UTC `DateTime` (fixed by moving calendar dates to `DateOnly`); in-memory employee search replaced by indexed server-side search.
