# Engineering Decisions & Tradeoffs

- **Modular monolith, not microservices.** One team, one database; the worker is split out only because async processing has a different scaling and failure profile.
- **EF Core DbSets behind `IApplicationDbContext` instead of repositories.** EF already provides Unit of Work and Repository; an extra layer would add code without decoupling. The tradeoff is that Application references EF Core (not Npgsql).
- **`ApplicationUser : IdentityUser` in Domain.** Pragmatic coupling to the existing Identity store, instead of maintaining a mirrored user model.
- **Cookie auth instead of JWT.** Same-origin SPA, so an HttpOnly cookie is safer than a JS-readable token, and existing Identity is reused. Requires CSRF protection (double-submit token implemented).
- **Outbox instead of publishing directly from the API.** Avoids dual-write inconsistency and keeps the API available during broker outages. Cost: notifications are delayed by seconds (poll interval).
- **Polling for notifications instead of SignalR.** Simpler, and the polled endpoint is Redis-cached. SignalR is a natural next step.
- **Offset pagination.** Fine at this scale with indexed sorts and a page-size cap. Keyset pagination would suit very deep pages.
- **xmin as concurrency token.** Free, needs no column, and changes on every update.
- **Enums as strings.** Readable SQL, safe against enum reordering, with CHECK constraints.
- **`lower()` + LIKE with trigram expression indexes** instead of Npgsql `ILIKE`. Keeps the Application layer provider-agnostic and is still index-backed. These indexes live in raw SQL inside the migration.
- **Rate limits are per-instance (in-memory).** A multi-instance deployment would need a distributed limiter (for example Redis-backed).
- **Not done:** automated tests (out of scope for this phase), SQLite → PostgreSQL data import (the seeder recreates the demo dataset), Azure deployment changes.
