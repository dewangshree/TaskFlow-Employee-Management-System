<div align="center">

# 🗂️ TaskFlow — Employee Task & Leave Management

### React + TypeScript · ASP.NET Core .NET 10 · PostgreSQL · Redis · RabbitMQ

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/React-19-61DAFB?style=for-the-badge&logo=react&logoColor=black)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5-3178C6?style=for-the-badge&logo=typescript&logoColor=white)](https://www.typescriptlang.org/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17-4169E1?style=for-the-badge&logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![Redis](https://img.shields.io/badge/Redis-Cache-DC382D?style=for-the-badge&logo=redis&logoColor=white)](https://redis.io/)
[![RabbitMQ](https://img.shields.io/badge/RabbitMQ-Messaging-FF6600?style=for-the-badge&logo=rabbitmq&logoColor=white)](https://www.rabbitmq.com/)
[![Azure](https://img.shields.io/badge/Deployed%20on-Azure-0078D4?style=for-the-badge&logo=microsoftazure&logoColor=white)](https://azure.microsoft.com/)

> **Admins** create and assign work, review leave and manage employees. **Employees** get a scoped self-service workspace for their own tasks and leave.
> Rebuilt from an ASP.NET MVC app into a React SPA + versioned REST API with asynchronous notifications, caching, rate limiting and optimistic concurrency.

🚀 **[Live Demo →](https://taskflow-shreyas-svd2026-c9bbcsezbmbjbvbu.centralindia-01.azurewebsites.net/)** · 📄 [API Docs (Swagger)](https://taskflow-shreyas-svd2026-c9bbcsezbmbjbvbu.centralindia-01.azurewebsites.net/swagger) · 🏛️ [Architecture Docs](docs/architecture/01-architecture-overview.md)

</div>

---

## 📋 Table of Contents

- [System Overview](#-system-overview)
- [Features](#-features)
- [Architecture](#-architecture)
- [Business Workflows](#-business-workflows)
- [System Design Concepts](#-system-design-concepts)
- [Security Model — RBAC + Ownership](#-security-model--rbac--ownership)
- [Database Design](#-database-design)
- [API Overview](#-api-overview)
- [Frontend Engineering](#-frontend-engineering)
- [Key Engineering Decisions](#-key-engineering-decisions)
- [Challenges & Solutions](#-challenges--solutions)
- [Local Setup](#-local-setup)
- [Project Structure](#-project-structure)

---

## 🔷 System Overview

| Area | Implementation |
|---|---|
| Frontend | React 19, TypeScript, Vite, Material UI, TanStack Query, React Hook Form + Zod, Axios |
| Backend | ASP.NET Core .NET 10 REST API, Clean Architecture (modular monolith) |
| Background processing | Worker: transactional outbox dispatcher + RabbitMQ notification consumer |
| Database | PostgreSQL (EF Core, migrations, indexes, constraints) with SQLite fallback for free hosting |
| Caching | Redis cache-aside with automatic database fallback |
| Messaging | RabbitMQ topic exchange, delayed retries, dead-letter queue, idempotent consumer |
| Auth | ASP.NET Core Identity (HttpOnly cookie) + CSRF protection + role policies |
| Observability | Serilog structured logs, correlation IDs, health checks, ProblemDetails errors |

---

## ✨ Features

| Module | Admin | Employee |
|---|---|---|
| 📊 **Dashboard** | Org-wide stats: active employees, open/overdue tasks, pending leave, on leave today, tasks by status | Open/overdue tasks, completion rate, leave days taken this year, upcoming deadlines |
| ✅ **Tasks** | Create, edit, assign, delete; filter, sort, search, paginate | View own tasks, move status (Pending → In Progress → Completed) |
| 🏖️ **Leave** | Review queue, approve/reject with comment | Apply, view history; overlapping requests blocked |
| 👥 **Employees** | Search, filter by department/status, activate/deactivate | — |
| 🔔 **Notifications** | New leave requests, task status changes | Task assigned, leave approved/rejected |
| 👤 **Profile** | Edit details, change password (current password required) | Same |

---

## 🏗 Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                  React SPA (TypeScript + MUI)                │
│     TanStack Query cache · lazy routes · debounced search    │
└──────────────────────────┬───────────────────────────────────┘
                           │  /api/v1  (HttpOnly cookie + X-XSRF-TOKEN)
                           ▼
┌──────────────────────────────────────────────────────────────┐
│                     TaskFlow.Api (.NET 10)                   │
│  Correlation ID → Rate Limiter → CSRF → AuthZ → Controllers  │
│              ProblemDetails · Swagger · Health checks        │
└────────────┬─────────────────────────────────┬───────────────┘
             │                                 │ cache-aside
             ▼                                 ▼
┌────────────────────────────┐      ┌─────────────────────────┐
│ Application Services       │      │ Redis                   │
│ Tasks · Leaves · Employees │      │ dashboards · lookups ·  │
│ Dashboard · Notifications  │      │ unread counts (TTL)     │
└────────────┬───────────────┘      └─────────────────────────┘
             ▼
┌──────────────────────────────────────────────────────────────┐
│ PostgreSQL — business rows + OutboxMessages (ONE transaction)│
└────────────┬─────────────────────────────────────────────────┘
             │ poll (FOR UPDATE SKIP LOCKED)
             ▼
┌──────────────────────────────────────────────────────────────┐
│ TaskFlow.Worker                                              │
│  OutboxPublisher ──► RabbitMQ (taskflow.events, topic)       │
│                         │  task.* / leave.*                  │
│                         ▼                                    │
│  NotificationConsumer ◄─ taskflow.notifications              │
│     retry.1 (5s) → retry.2 (30s) → retry.3 (2m) → .dlq       │
│  Notifications + ProcessedMessages (idempotent, one tx)      │
└──────────────────────────────────────────────────────────────┘
```

| Layer | Project | Responsibility |
|---|---|---|
| Domain | `src/TaskFlow.Domain` | Entities with behaviour, status-transition rules, domain events |
| Application | `src/TaskFlow.Application` | Use-case services, DTOs, FluentValidation, cache keys, notification handler |
| Infrastructure | `src/TaskFlow.Infrastructure` | EF Core, migrations, outbox, Redis, RabbitMQ, health checks, seeding |
| API | `src/TaskFlow.Api` | Versioned controllers, middleware, auth, rate limiting, OpenAPI |
| Worker | `src/TaskFlow.Worker` | Hosts the outbox dispatcher and RabbitMQ consumer |

---

## 🔁 Business Workflows

**Task assignment**

```
Admin creates task
      │
      ▼
FluentValidation (title, due date, active assignee)
      │
      ▼
TaskItem.Create() raises TaskAssignedEvent
      │
      ▼
SaveChanges: task + outbox row committed in ONE transaction
      │
      ├──► Redis: admin + employee dashboard keys invalidated
      ▼
Worker publishes "task.assigned" to RabbitMQ (publisher confirms)
      │
      ▼
Consumer creates notification (idempotent) → unread-count cache dropped
      │
      ▼
React bell icon picks it up on the next poll
```

**Leave approval**

```
Employee applies ──► overlap check ──► LeaveRequestedEvent ──► all admins notified
                                                │
Admin reviews (sends the version it loaded)     ▼
      │
      ▼
Domain rule: only Pending can be reviewed  +  optimistic concurrency (xmin)
      │
      ▼
LeaveReviewedEvent ──► employee notified ──► dashboards refreshed
```

---

## ⚙️ System Design Concepts

| Concept | What was built | Code |
|---|---|---|
| 🚦 **Rate limiting** | Token bucket (global, per user/IP), fixed window on login/register (per IP), sliding window on search endpoints; 429 + `Retry-After`; configurable | [`RateLimitingSetup.cs`](src/TaskFlow.Api/Configuration/RateLimitingSetup.cs) |
| ⚡ **Redis caching** | Cache-aside, TTLs, user-scoped keys, invalidation after writes, circuit breaker → PostgreSQL fallback | [`RedisCacheService.cs`](src/TaskFlow.Infrastructure/Caching/RedisCacheService.cs) |
| 📨 **Transactional outbox** | Domain events saved with the business change in the same transaction, published later | [`AppDbContext.cs`](src/TaskFlow.Infrastructure/Persistence/AppDbContext.cs) |
| 🐇 **RabbitMQ** | Topic exchange, publisher confirms, 3-tier delayed retry queues, dead-letter queue, poison-message handling | [`Messaging/`](src/TaskFlow.Infrastructure/Messaging) |
| ♻️ **Idempotent consumer** | `ProcessedMessages` inbox + unique `(UserId, SourceEventId)` index | [`NotificationMessageProcessor.cs`](src/TaskFlow.Infrastructure/Messaging/NotificationMessageProcessor.cs) |
| 🔒 **Optimistic concurrency** | PostgreSQL `xmin` version on tasks and leave requests → 409 on stale updates | [`Configurations/`](src/TaskFlow.Infrastructure/Persistence/Configurations) |
| 🗄️ **DB optimization** | Composite, partial and pg_trgm GIN indexes, check constraints, `AsNoTracking`, projections, no N+1 | [Database Design](docs/architecture/03-database-design.md) |
| 📄 **Scalable pagination** | Server-side filter, sort (whitelisted), search and paging; page size capped at 100 | [`Paging.cs`](src/TaskFlow.Application/Common/Paging.cs) |
| 🩺 **Reliability** | `/health/live`, `/health/ready` (database, Redis, outbox backlog), correlation IDs across HTTP → queue → worker | [`Program.cs`](src/TaskFlow.Api/Program.cs) |

Detailed write-ups: [Redis](docs/architecture/05-redis-design.md) · [RabbitMQ](docs/architecture/06-rabbitmq-design.md) · [API](docs/architecture/04-api-design.md) · [Decisions](docs/architecture/08-engineering-decisions.md)

---

## 🔐 Security Model — RBAC + Ownership

Two roles (**Admin**, **Employee**) via ASP.NET Core Identity, enforced server-side with policies. Every endpoint requires authentication unless explicitly marked public.

```
[Authorize(Policy = Policies.AdminOnly)]     → employees, all tasks, task create/delete, leave review
[Authorize(Policy = Policies.EmployeeOnly)]  → my tasks, apply leave, my dashboard
Fallback policy                              → authenticated user required everywhere else
```

```
✅ Ownership checks in queries: an employee can only see/update their own tasks and leave (others → 404)
✅ Only admins can cancel or reinstate tasks; invalid status transitions → 409
✅ HttpOnly, SameSite=Strict auth cookie + double-submit CSRF token on every state-changing request
✅ Account lockout after 5 failed logins; deactivated users lose access within 1 minute (security stamp)
✅ Password change requires the current password
```

---

## 🗄 Database Design

```
AspNetUsers (ApplicationUser)  (1)
    │
    ├── AssignedTasks  (*)  ──►  TaskItems.AssignedToId       [SetNull]
    ├── CreatedTasks   (*)  ──►  TaskItems.CreatedById        [Restrict]
    ├── LeaveRequests  (*)  ──►  LeaveRequests.EmployeeId     [Cascade]
    ├── Reviewed       (*)  ──►  LeaveRequests.ReviewedById   [SetNull]
    └── Notifications  (*)  ──►  Notifications.UserId         [Cascade]

OutboxMessages      (jsonb payload, partial index on unprocessed rows)
ProcessedMessages   (PK: MessageId + Consumer)
```

| Index | Serves |
|---|---|
| `TaskItems (AssignedToId, Status)` | My tasks, employee dashboard counts |
| `TaskItems (Status, DueDate)` | Status filter, overdue detection |
| `LeaveRequests (Status, AppliedOn DESC)` | Admin review queue |
| `LeaveRequests (EmployeeId, StartDate, EndDate)` | Overlap check |
| `Notifications (UserId, IsRead, CreatedAt DESC)` | Notification list + unread badge |
| GIN `pg_trgm` on `lower(name / email / title …)` | Case-insensitive "contains" search |

Full list and rationale: [docs/architecture/03-database-design.md](docs/architecture/03-database-design.md)

---

## 🔌 API Overview

Base path **`/api/v1`** (URL-segment versioning). Errors use RFC 9457 **ProblemDetails** with `code` and `correlationId`.

| Method | Endpoint | Access |
|---|---|---|
| `POST` | `/auth/login` · `/auth/register` · `/auth/logout` | Public (rate limited) / authenticated |
| `GET` | `/dashboard/admin` · `/dashboard/me` | Admin · Employee |
| `GET` `POST` `PUT` `DELETE` | `/tasks` · `/tasks/{id}` | Admin |
| `GET` | `/tasks/assigned-to-me` | Employee |
| `PATCH` | `/tasks/{id}/status` | Admin or assignee |
| `POST` `GET` | `/leaves` · `/leaves/mine` | Employee |
| `GET` `PUT` | `/leaves` · `/leaves/{id}/review` | Admin |
| `GET` `PUT` | `/employees` · `/employees/{id}/active` | Admin |
| `GET` `POST` | `/notifications` · `/notifications/unread-count` · `/read-all` | Authenticated (own data) |
| `GET` | `/health/live` · `/health/ready` | Public |

Interactive docs at **`/swagger`**.

---

## ⚛️ Frontend Engineering

| Technique | Where |
|---|---|
| `useReducer` | Auth state machine; table state (search/filter/sort reset page to 1) |
| `useContext` | Authentication, toast notifications |
| `useMemo` / `useCallback` + `React.memo` | Memoised table rows: typing in search doesn't re-render every row |
| `useRef` | Press `/` to focus search; refocus password after failed login |
| Custom hooks | `useDebounce`, `useListParams`, `useDocumentTitle`, `useUpdateTaskStatus` |
| `React.lazy` + `Suspense` | Every page is its own chunk |
| Error boundaries | Global + per route (resets on navigation) |
| Optimistic updates | Task status (rolled back on 409), employee activation, mark notification read |
| Debounced search | 350 ms, paired with the server's search rate limit |

More: [docs/architecture/07-react-concepts.md](docs/architecture/07-react-concepts.md)

---

## 🧠 Key Engineering Decisions

| Decision | Rationale |
|---|---|
| Modular monolith, not microservices | One team, one database; only async processing is split out |
| Transactional outbox instead of publishing from the API | No lost or phantom events; API stays up even if RabbitMQ is down |
| Cookie auth instead of JWT in localStorage | Same-origin SPA; HttpOnly cookie can't be stolen by XSS; reuses Identity |
| `xmin` as concurrency token | Free in PostgreSQL, no extra column, changes on every update |
| Redis is optional, never required | Circuit breaker + DB fallback; outage shows as *Degraded*, not down |
| SQLite fallback + in-process worker | Runs on a single free App Service instance with no paid services; Postgres/Redis/RabbitMQ switch on via config |
| Polling for notifications instead of SignalR | Simpler; the polled endpoint is Redis-cached |

---

## 🛠 Challenges & Solutions

**1. Events need ids that don't exist yet**

```
Issue:    Domain events are raised in TaskItem.Create(), but PostgreSQL only
          assigns the task id on INSERT, so events would carry id = 0
Solution: Events are stored as factories and materialised after the first
          SaveChanges, inside the same transaction as the outbox insert
```

**2. Two admins approving the same leave**

```
Issue:    Both load a Pending request and click Approve at the same time
Solution: Domain rule (only Pending can be reviewed) + optimistic concurrency
          on xmin; the second save gets 409 Conflict instead of overwriting
```

**3. Duplicate notifications on redelivery**

```
Issue:    RabbitMQ delivers at-least-once; a crash after processing but before
          ack would create the notification twice
Solution: Inbox table (MessageId, Consumer) written in the same transaction as
          the notifications, plus a unique (UserId, SourceEventId) index
```

**4. Employee search loaded the whole table**

```
Issue:    The MVC version fetched every employee and filtered in memory
Solution: Server-side filtering/paging with trigram GIN indexes on
          lower(column) and term-by-term matching ("jane design")
```

**5. CSRF token invalid right after login**

```
Issue:    Antiforgery tokens are bound to the user identity, so the token
          issued before login fails on the first request after it
Solution: Issue a fresh token for the new principal in the login response
```

---

## 🚀 Local Setup

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)
- PostgreSQL, Redis, RabbitMQ are **optional**: without them the app uses SQLite, reads directly from the database, and processes notifications in-process

### Quick start (no external services)

```bash
git clone https://github.com/dewangshree/TaskFlow-Employee-Management-System.git
cd TaskFlow-Employee-Management-System

# Build the React app into the API's wwwroot
cd frontend && npm install && npm run build && cd ..
cp -r frontend/dist src/TaskFlow.Api/wwwroot

# Run (SQLite + in-process worker, demo data seeded automatically)
ASPNETCORE_ENVIRONMENT=Production dotnet run --project src/TaskFlow.Api --urls http://localhost:5080
```

Open **http://localhost:5080** · Swagger at **/swagger**

### Full stack (PostgreSQL + Redis + RabbitMQ)

```bash
dotnet tool restore
dotnet run --project src/TaskFlow.Api       # http://localhost:5080, applies migrations in Development
dotnet run --project src/TaskFlow.Worker    # outbox publisher + RabbitMQ consumer
cd frontend && npm run dev                  # http://localhost:5173 (proxies /api)
```

Connection strings: `src/TaskFlow.Api/appsettings.Development.json` and `src/TaskFlow.Worker/appsettings.Development.json`, or environment variables (`ConnectionStrings__Postgres`, `ConnectionStrings__Redis`, `ConnectionStrings__RabbitMq`).

### Migrations

```bash
dotnet tool run dotnet-ef migrations add <Name> -p src/TaskFlow.Infrastructure -s src/TaskFlow.Api -o Persistence/Migrations
dotnet tool run dotnet-ef database update -p src/TaskFlow.Infrastructure -s src/TaskFlow.Api
```

### Demo Credentials

| Role | Email | Password |
|---|---|---|
| Admin | admin@taskflow.com | Admin@123 |
| Employee | john.doe@taskflow.com | Employee@123 |
| Employee | jane.smith@taskflow.com | Employee@123 |
| Employee | mike.johnson@taskflow.com | Employee@123 |

---

## 📁 Project Structure

```
src/
  TaskFlow.Domain/          entities, enums, domain events, business rules
  TaskFlow.Application/     services, DTOs, validators, caching contracts
  TaskFlow.Infrastructure/  EF Core + migrations, outbox, Redis, RabbitMQ, health checks
  TaskFlow.Api/             controllers, middleware, auth, rate limiting, Program.cs
  TaskFlow.Worker/          background host (outbox dispatcher + consumer)
frontend/
  src/api/                  typed Axios client, endpoints, query keys
  src/auth/                 AuthContext (useReducer), route guards
  src/components/           app shell, notification bell, tables, dialogs, error boundary
  src/features/             dashboard, tasks, leaves, employees, profile, notifications
  src/hooks/                useDebounce, useListParams, useNotify, useDocumentTitle
docs/architecture/          9 design documents (system design, DB, API, Redis, RabbitMQ, React, decisions)
```

---

<div align="center">

Built by **Shreyas Dewangswami** · ASP.NET Core · React · PostgreSQL · Redis · RabbitMQ

</div>
