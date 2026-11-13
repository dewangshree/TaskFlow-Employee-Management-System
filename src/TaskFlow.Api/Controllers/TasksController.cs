using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskFlow.Api.Configuration;
using TaskFlow.Api.Security;
using TaskFlow.Application.Common;
using TaskFlow.Application.Features.Tasks;

namespace TaskFlow.Api.Controllers;

public sealed class TasksController(ITaskService tasks) : ApiControllerBase
{
    /// <summary>All tasks (admin). Server-side filtering, sorting and pagination.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.AdminOnly)]
    [EnableRateLimiting(RateLimitPolicies.Search)]
    [ProducesResponseType(typeof(PagedResult<TaskListItemDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<TaskListItemDto>> List([FromQuery] TaskQuery query, CancellationToken ct) => tasks.ListAsync(query, ct);

    /// <summary>Tasks assigned to the signed-in employee.</summary>
    [HttpGet("assigned-to-me")]
    [Authorize(Policy = Policies.EmployeeOnly)]
    [EnableRateLimiting(RateLimitPolicies.Search)]
    [ProducesResponseType(typeof(PagedResult<TaskListItemDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<TaskListItemDto>> AssignedToMe([FromQuery] TaskQuery query, CancellationToken ct) =>
        tasks.ListAssignedToCurrentUserAsync(query, ct);

    /// <summary>Task detail. Admins see any task; employees only their own (404 otherwise).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TaskDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<TaskDetailDto> Get(int id, CancellationToken ct) => tasks.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType(typeof(TaskDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateTaskRequest request, CancellationToken ct)
    {
        var created = await tasks.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id, version = RequestedVersion }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType(typeof(TaskDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<TaskDetailDto> Update(int id, UpdateTaskRequest request, CancellationToken ct) => tasks.UpdateAsync(id, request, ct);

    [HttpPut("{id:int}/assignee")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType(typeof(TaskDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<TaskDetailDto> Assign(int id, AssignTaskRequest request, CancellationToken ct) => tasks.AssignAsync(id, request, ct);

    /// <summary>Change status. Transition rules and ownership are enforced server-side.</summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(typeof(TaskDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<TaskDetailDto> UpdateStatus(int id, UpdateTaskStatusRequest request, CancellationToken ct) =>
        tasks.UpdateStatusAsync(id, request, ct);

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await tasks.DeleteAsync(id, ct);
        return NoContent();
    }
}
