# Architecture Overview

Modular monolith using pragmatic Clean Architecture. Two deployable processes share one codebase.

| Project | Responsibility |
|---|---|
| `src/TaskFlow.Domain` | Entities with behaviour (`TaskItem`, `LeaveRequest`, `Notification`), status-transition rules, domain events, outbox/inbox entities. No EF or ASP.NET dependencies (except `IdentityUser` base, see decisions). |
| `src/TaskFlow.Application` | Use-case services (`TaskService`, `LeaveService`, `EmployeeService`, `DashboardService`, `ProfileService`, `NotificationService`), DTOs, FluentValidation validators, cache keys, `NotificationEventHandler`. Depends on abstractions (`IApplicationDbContext`, `ICacheService`, `ICurrentUser`). |
| `src/TaskFlow.Infrastructure` | `AppDbContext` (Npgsql), entity configurations/indexes, migrations, outbox writer, seeder, Redis cache-aside, RabbitMQ topology/publisher/dispatcher/consumer processor, health checks. |
| `src/TaskFlow.Api` | Versioned REST controllers, Identity cookie auth + CSRF, ProblemDetails exception handler, rate limiting, correlation-id middleware, Serilog, OpenAPI/Swagger UI, health endpoints. |
| `src/TaskFlow.Worker` | `OutboxPublisherService` (outbox → RabbitMQ) and `NotificationConsumerService` (RabbitMQ → notifications). |
| `frontend/` | React 19 + TypeScript + Vite SPA (MUI, TanStack Query, React Hook Form + Zod, Axios). |

Request path: React → (Vite proxy / nginx) → API → Application service → PostgreSQL (+ Redis). Writes that matter to other users also write an outbox row; the worker publishes and consumes it asynchronously.
