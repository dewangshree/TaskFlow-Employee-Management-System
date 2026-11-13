# System Design Diagram

```
 Browser (React SPA)
   │  same-origin /api  (HttpOnly auth cookie + X-XSRF-TOKEN header)
   ▼
 nginx / Vite proxy
   ▼
 TaskFlow.Api ──── rate limiter ── CSRF ── authZ ── controllers ── services
   │        │                                                    │
   │        └── Redis (cache-aside: dashboards, lookups, unread counts; fallback to DB)
   ▼
 PostgreSQL  ◄── business rows + OutboxMessages written in ONE transaction
   ▲    │
   │    │ poll (FOR UPDATE SKIP LOCKED)
   │    ▼
   │  TaskFlow.Worker: OutboxPublisherService ──publish+confirm──► RabbitMQ exchange taskflow.events (topic)
   │                                                                    │ task.* / leave.*
   │                                                                    ▼
   │                                              queue taskflow.notifications ──► retry.1 (5s) / retry.2 (30s) / retry.3 (2m)
   │                                                                    │                     └─► .dlq after max retries / poison
   │  NotificationConsumerService ◄─────────────────────────────────────┘
   └── Notifications + ProcessedMessages (one transaction) ──► Redis: drop unread-count keys
```

Task assignment flow: admin `POST /api/v1/tasks` → validator → `TaskItem.Create` raises `TaskAssignedEvent` → `AppDbContext.SaveChangesAsync` saves task + outbox row atomically → API drops dashboard cache keys → worker publishes `task.assigned` → consumer creates a notification for the assignee (idempotently) and drops their unread-count key → SPA bell polls `/notifications/unread-count` (30s) and shows it.

Leave approval flow: employee `POST /leaves` (overlap check) → `LeaveRequestedEvent` → admins notified. Admin `PUT /leaves/{id}/review` with `version` → domain rule "only Pending can be reviewed" + xmin concurrency check → `LeaveReviewedEvent` → employee notified; dashboards invalidated.
