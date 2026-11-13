# API Design

Base path `/api/v1` (URL-segment versioning via Asp.Versioning, with an `api-supported-versions` header). OpenAPI is at `/openapi/v1.json`; Swagger UI is at `/swagger` (Development, or when `OpenApi:Enabled=true`).

| Method | Path | Access |
|---|---|---|
| GET | /auth/csrf | anonymous: issues XSRF-TOKEN cookie |
| POST | /auth/login, /auth/register | anonymous, `auth` rate limit |
| POST | /auth/logout · GET /auth/me | authenticated |
| GET | /dashboard/admin · /dashboard/me | Admin · Employee |
| GET | /tasks | Admin (`search` limit): page, pageSize, search, status, priority, assigneeId, overdueOnly, sortBy, sortDirection |
| GET | /tasks/assigned-to-me | Employee |
| GET | /tasks/{id} | Admin, or the assignee (otherwise 404) |
| POST · PUT · DELETE | /tasks · /tasks/{id} | Admin (PUT requires `version`) |
| PUT | /tasks/{id}/assignee | Admin |
| PATCH | /tasks/{id}/status | Admin, or the assignee; transition rules enforced |
| POST · GET | /leaves · /leaves/mine | Employee |
| GET | /leaves · /leaves/{id} | Admin · Admin or owner |
| PUT | /leaves/{id}/review | Admin, `{decision, comment, version}` |
| GET · PUT | /employees · /employees/{id}/active | Admin |
| GET | /employees/assignable · /employees/departments | Admin (Redis-cached) |
| GET · PUT | /profile · /profile/password | authenticated (password change requires current password) |
| GET · POST | /notifications, /notifications/unread-count, /{id}/read, /read-all | authenticated, own data only |
| GET | /health/live · /health/ready | anonymous |

**Security:** ASP.NET Core Identity cookie (HttpOnly, SameSite=Strict, Secure outside dev) with a fallback policy that requires authentication. Role policies `AdminOnly`/`EmployeeOnly`; ownership is enforced inside service queries. CSRF uses a double-submit token (`CsrfValidationMiddleware`). Lockout after 5 failed logins. Security stamp validation runs every minute, so deactivated users are signed out.

**Errors:** RFC 9457 ProblemDetails with `code`, `traceId`, `correlationId`. 400 validation (`errors`), 401, 403, 404, 409 (`concurrency`, `leave.already_reviewed`, `task.invalid_transition`, `leave.overlap`), 429 (`Retry-After` header plus `retryAfterSeconds`).

**Rate limiting** (`Configuration/RateLimitingSetup.cs`, configurable under `RateLimiting`): global token bucket per user/IP (300/min); `auth` fixed window per IP (10/min); `search` sliding window per user (60/min); health endpoints exempt.
