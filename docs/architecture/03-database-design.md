# Database Design (PostgreSQL)

Migration: `src/TaskFlow.Infrastructure/Persistence/Migrations/*_InitialPostgres.cs`. Configurations: `Persistence/Configurations/`.

## Relationships
- `AspNetUsers 1─* TaskItems.AssignedToId` (SET NULL), `1─* TaskItems.CreatedById` (RESTRICT): same as the original app
- `AspNetUsers 1─* LeaveRequests.EmployeeId` (CASCADE), `ReviewedById` (SET NULL, new)
- `AspNetUsers 1─* Notifications.UserId` (CASCADE)
- `OutboxMessages` (jsonb payload), `ProcessedMessages` (PK `MessageId, Consumer`)

## Types and constraints
- Calendar dates (`DueDate`, `StartDate`, `EndDate`) are `date` (`DateOnly`); timestamps are UTC `timestamptz`.
- Enums are stored as strings, with CHECK constraints on allowed values.
- `CK_LeaveRequests_DateRange` (`EndDate >= StartDate`), `CK_LeaveRequests_ReviewedBy` (a decided request must have `ReviewedAt`), `CK_TaskItems_Title_NotBlank`.
- Optimistic concurrency: `TaskItem.Version` / `LeaveRequest.Version` are mapped to PostgreSQL's `xmin` system column.

## Indexes and the query each one serves
| Index | Query |
|---|---|
| `TaskItems(AssignedToId, Status)` | My tasks, employee dashboard counts, employee open-task counts |
| `TaskItems(Status, DueDate)` | Status filter, overdue detection |
| `TaskItems(CreatedAt DESC)` | Default admin list order, recent tasks |
| `LeaveRequests(EmployeeId, AppliedOn DESC)` | Employee leave history |
| `LeaveRequests(Status, AppliedOn DESC)` | Admin review queue, status counts |
| `LeaveRequests(EmployeeId, StartDate, EndDate)` | Overlap check on apply |
| `Notifications(UserId, IsRead, CreatedAt DESC)` | Notification list and unread badge |
| `Notifications(UserId, SourceEventId)` UNIQUE | Idempotency backstop |
| `OutboxMessages(OccurredAt) WHERE ProcessedAt IS NULL` (partial) | Dispatcher scan stays small as history grows |
| `AspNetUsers(Department)`, `(FirstName, LastName)` | Department filter/lookup, name sort |
| GIN `pg_trgm` on `lower(FirstName/LastName/Email/Department)`, `lower(TaskItems.Title/Description)`, `lower(LeaveRequests.Reason)` | `lower(col) LIKE '%term%'` search |

## Query practices
- `AsNoTracking()` on all reads; DTO projections with `Select` (joins in one SQL statement, so no N+1). Employee task counts are correlated subqueries.
- Server-side filtering, whitelisted sorting and `Skip/Take` paging (`Common/Paging.cs`, page size capped at 100).
- Search splits input into terms (AND of ORs), escapes `%`/`_`, and each predicate can use a trigram index.
- `ExecuteUpdateAsync` for mark-all-read; `ExecuteDeleteAsync` for outbox retention.
- Transactions: aggregate + outbox in one transaction (`AppDbContext.SaveChangesAsync`); consumer notifications + inbox row in one transaction.
