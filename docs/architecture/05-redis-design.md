# Redis Design

Implementation: `Infrastructure/Caching/RedisCacheService.cs`, `RedisConnection.cs`; keys are in `Application/Caching/CacheKeys.cs`.

| Key | Content | TTL | Invalidated by |
|---|---|---|---|
| `taskflow:v1:dashboard:admin` | Admin dashboard | 120s | any task/leave write, employee activation, registration, profile update |
| `taskflow:v1:dashboard:employee:{userId}` | Employee dashboard | 120s | writes affecting that user's tasks/leaves (old and new assignee) |
| `taskflow:v1:ref:assignable-employees` | Active employee lookup | 600s | activation toggle, registration, profile update |
| `taskflow:v1:ref:departments` | Distinct departments | 600s | same |
| `taskflow:v1:notifications:unread:{userId}` | Unread count (polled every 30s per tab) | 300s | worker after creating notifications; mark-read in the API |

- Cache-aside via `GetOrCreateAsync`. Invalidation happens after the DB commit, and the TTL bounds staleness if an invalidation is missed.
- User isolation: every user-scoped key embeds the user id. The `v1` segment allows invalidating everything when a DTO shape changes.
- Failure handling: `AbortOnConnectFail=false`, 1s timeouts, and a 30s circuit breaker after any Redis error. All reads fall back to PostgreSQL. `/health/ready` reports Redis outages as Degraded, not Unhealthy.
- Not implemented (tradeoff): stampede protection / single-flight. Acceptable at current load.
