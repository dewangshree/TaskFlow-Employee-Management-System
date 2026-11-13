using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Abstractions;
using TaskFlow.Application.Caching;
using TaskFlow.Application.Common;
using TaskFlow.Application.Features.Employees;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Tasks;

public interface ITaskService
{
    Task<PagedResult<TaskListItemDto>> ListAsync(TaskQuery query, CancellationToken ct);
    Task<PagedResult<TaskListItemDto>> ListAssignedToCurrentUserAsync(TaskQuery query, CancellationToken ct);
    Task<TaskDetailDto> GetAsync(int id, CancellationToken ct);
    Task<TaskDetailDto> CreateAsync(CreateTaskRequest request, CancellationToken ct);
    Task<TaskDetailDto> UpdateAsync(int id, UpdateTaskRequest request, CancellationToken ct);
    Task<TaskDetailDto> AssignAsync(int id, AssignTaskRequest request, CancellationToken ct);
    Task<TaskDetailDto> UpdateStatusAsync(int id, UpdateTaskStatusRequest request, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public sealed class TaskService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ICacheService cache,
    IEmployeeDirectory employees,
    TimeProvider time) : ITaskService
{
    public Task<PagedResult<TaskListItemDto>> ListAsync(TaskQuery query, CancellationToken ct)
    {
        var tasks = db.TaskItems.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.AssigneeId))
            tasks = tasks.Where(t => t.AssignedToId == query.AssigneeId);

        return ProjectPageAsync(ApplyFilters(tasks, query, includeAssigneeSearch: true), query, ct);
    }

    public Task<PagedResult<TaskListItemDto>> ListAssignedToCurrentUserAsync(TaskQuery query, CancellationToken ct)
    {
        var userId = currentUser.RequiredUserId;
        // Ownership is enforced in the query itself, so an employee can never page through other people's tasks.
        var tasks = db.TaskItems.AsNoTracking().Where(t => t.AssignedToId == userId);
        return ProjectPageAsync(ApplyFilters(tasks, query, includeAssigneeSearch: false), query, ct);
    }

    public async Task<TaskDetailDto> GetAsync(int id, CancellationToken ct)
    {
        var task = await db.TaskItems.AsNoTracking()
            .Include(t => t.AssignedTo)
            .Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Task", id);

        EnsureCanView(task);
        return ToDetail(task);
    }

    public async Task<TaskDetailDto> CreateAsync(CreateTaskRequest request, CancellationToken ct)
    {
        await EnsureAssignableAsync(request.AssigneeId, ct);

        var task = TaskItem.Create(
            request.Title,
            request.Description ?? string.Empty,
            request.Priority,
            request.DueDate,
            currentUser.RequiredUserId,
            request.AssigneeId,
            time.UtcNow());

        db.TaskItems.Add(task);
        await db.SaveChangesAsync(ct); // task + outbox event committed atomically

        await InvalidateDashboardsAsync(task.AssignedToId);
        return await GetAsync(task.Id, ct);
    }

    public async Task<TaskDetailDto> UpdateAsync(int id, UpdateTaskRequest request, CancellationToken ct)
    {
        var task = await FindTrackedAsync(id, ct);
        db.SetExpectedVersion(task, request.Version);

        var previousAssignee = task.AssignedToId;
        if (request.AssigneeId != previousAssignee)
            await EnsureAssignableAsync(request.AssigneeId, ct);

        var now = time.UtcNow();
        var actorId = currentUser.RequiredUserId;
        task.UpdateDetails(request.Title, request.Description ?? string.Empty, request.Priority, request.DueDate, now);
        task.AssignTo(request.AssigneeId, actorId, now);
        task.ChangeStatus(request.Status, actorId, changedByAdmin: true, now);

        await db.SaveChangesAsync(ct);
        await InvalidateDashboardsAsync(previousAssignee, task.AssignedToId);
        return await GetAsync(id, ct);
    }

    public async Task<TaskDetailDto> AssignAsync(int id, AssignTaskRequest request, CancellationToken ct)
    {
        var task = await FindTrackedAsync(id, ct);
        db.SetExpectedVersion(task, request.Version);
        await EnsureAssignableAsync(request.AssigneeId, ct);

        var previousAssignee = task.AssignedToId;
        task.AssignTo(request.AssigneeId, currentUser.RequiredUserId, time.UtcNow());

        await db.SaveChangesAsync(ct);
        await InvalidateDashboardsAsync(previousAssignee, task.AssignedToId);
        return await GetAsync(id, ct);
    }

    public async Task<TaskDetailDto> UpdateStatusAsync(int id, UpdateTaskStatusRequest request, CancellationToken ct)
    {
        var task = await FindTrackedAsync(id, ct);
        var isAdmin = currentUser.IsAdmin;

        // Employees may only update tasks assigned to them. A 404 (not 403) avoids
        // revealing that a task with this id exists.
        if (!isAdmin && task.AssignedToId != currentUser.UserId)
            throw new NotFoundException("Task", id);

        db.SetExpectedVersion(task, request.Version);
        task.ChangeStatus(request.Status, currentUser.RequiredUserId, isAdmin, time.UtcNow());

        await db.SaveChangesAsync(ct);
        await InvalidateDashboardsAsync(task.AssignedToId);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var task = await FindTrackedAsync(id, ct);
        db.TaskItems.Remove(task);
        await db.SaveChangesAsync(ct);
        await InvalidateDashboardsAsync(task.AssignedToId);
    }

    private async Task<TaskItem> FindTrackedAsync(int id, CancellationToken ct) =>
        await db.TaskItems.FirstOrDefaultAsync(t => t.Id == id, ct)
        ?? throw new NotFoundException("Task", id);

    private void EnsureCanView(TaskItem task)
    {
        if (!currentUser.IsAdmin && task.AssignedToId != currentUser.UserId)
            throw new NotFoundException("Task", task.Id);
    }

    private async Task EnsureAssignableAsync(string? assigneeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(assigneeId))
            return;

        if (!await employees.IsActiveEmployeeAsync(assigneeId, ct))
            throw new RequestValidationException("assigneeId", "Tasks can only be assigned to active employees.");
    }

    private Task InvalidateDashboardsAsync(params string?[] affectedUserIds)
    {
        var keys = affectedUserIds
            .Where(id => id is not null)
            .Distinct()
            .Select(id => CacheKeys.EmployeeDashboard(id!))
            .Append(CacheKeys.AdminDashboard);
        return cache.RemoveAsync(keys, CancellationToken.None);
    }

    private IQueryable<TaskItem> ApplyFilters(IQueryable<TaskItem> tasks, TaskQuery query, bool includeAssigneeSearch)
    {
        if (query.Status.HasValue)
            tasks = tasks.Where(t => t.Status == query.Status);

        if (query.Priority.HasValue)
            tasks = tasks.Where(t => t.Priority == query.Priority);

        if (query.OverdueOnly == true)
        {
            var today = time.Today();
            tasks = tasks.Where(t => t.DueDate < today &&
                (t.Status == TaskItemStatus.Pending || t.Status == TaskItemStatus.InProgress));
        }

        // Every search term must match at least one field (AND of ORs), so "jane design"
        // finds Jane's design tasks. Each predicate is backed by a trigram index.
        foreach (var pattern in SearchPattern.Terms(query.Search))
        {
            tasks = includeAssigneeSearch
                ? tasks.Where(t =>
                    EF.Functions.Like(t.Title.ToLower(), pattern, SearchPattern.EscapeCharacter) ||
                    EF.Functions.Like(t.Description.ToLower(), pattern, SearchPattern.EscapeCharacter) ||
                    (t.AssignedTo != null && (
                        EF.Functions.Like(t.AssignedTo.FirstName.ToLower(), pattern, SearchPattern.EscapeCharacter) ||
                        EF.Functions.Like(t.AssignedTo.LastName.ToLower(), pattern, SearchPattern.EscapeCharacter))))
                : tasks.Where(t =>
                    EF.Functions.Like(t.Title.ToLower(), pattern, SearchPattern.EscapeCharacter) ||
                    EF.Functions.Like(t.Description.ToLower(), pattern, SearchPattern.EscapeCharacter));
        }

        var dir = query.SortDirection;
        return (query.SortBy?.ToLowerInvariant()) switch
        {
            "title" => tasks.OrderByDirection(t => t.Title, dir).ThenBy(t => t.Id),
            "priority" => tasks.OrderByDirection(t => t.Priority, dir).ThenBy(t => t.Id),
            "status" => tasks.OrderByDirection(t => t.Status, dir).ThenBy(t => t.Id),
            "duedate" => tasks.OrderByDirection(t => t.DueDate, dir).ThenBy(t => t.Id),
            "assignee" => tasks.OrderByDirection(t => t.AssignedTo!.FirstName, dir).ThenBy(t => t.Id),
            _ => tasks.OrderByDirection(t => t.CreatedAt, dir).ThenBy(t => t.Id),
        };
    }

    private Task<PagedResult<TaskListItemDto>> ProjectPageAsync(IQueryable<TaskItem> tasks, TaskQuery query, CancellationToken ct)
    {
        var today = time.Today();
        // Projection selects only the columns needed and joins users in the same SQL
        // statement, so there is no N+1 lookup of assignee/creator names.
        return tasks
            .Select(t => new TaskListItemDto(
                t.Id,
                t.Title,
                t.Priority,
                t.Status,
                t.DueDate,
                t.DueDate < today && (t.Status == TaskItemStatus.Pending || t.Status == TaskItemStatus.InProgress),
                t.AssignedToId,
                t.AssignedTo != null ? t.AssignedTo.FirstName + " " + t.AssignedTo.LastName : null,
                t.CreatedBy != null ? t.CreatedBy.FirstName + " " + t.CreatedBy.LastName : string.Empty,
                t.CreatedAt,
                t.Version))
            .ToPagedResultAsync(query.SafePage, query.SafePageSize, ct);
    }

    private TaskDetailDto ToDetail(TaskItem t) => new(
        t.Id,
        t.Title,
        t.Description,
        t.Priority,
        t.Status,
        t.DueDate,
        t.IsOverdue(time.Today()),
        t.AssignedToId,
        t.AssignedTo?.FullName,
        t.CreatedById,
        t.CreatedBy?.FullName ?? string.Empty,
        t.CreatedAt,
        t.UpdatedAt,
        t.Version,
        TaskItem.TransitionsFrom(t.Status, currentUser.IsAdmin));
}
