using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskFlow.Api.Configuration;
using TaskFlow.Api.Security;
using TaskFlow.Application.Common;
using TaskFlow.Application.Features.Leaves;

namespace TaskFlow.Api.Controllers;

public sealed class LeavesController(ILeaveService leaves) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Policy = Policies.EmployeeOnly)]
    [ProducesResponseType(typeof(LeaveDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Apply(ApplyLeaveRequest request, CancellationToken ct)
    {
        var created = await leaves.ApplyAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id, version = RequestedVersion }, created);
    }

    [HttpGet("mine")]
    [Authorize(Policy = Policies.EmployeeOnly)]
    [EnableRateLimiting(RateLimitPolicies.Search)]
    [ProducesResponseType(typeof(PagedResult<LeaveDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<LeaveDto>> Mine([FromQuery] LeaveQuery query, CancellationToken ct) => leaves.ListMineAsync(query, ct);

    [HttpGet]
    [Authorize(Policy = Policies.AdminOnly)]
    [EnableRateLimiting(RateLimitPolicies.Search)]
    [ProducesResponseType(typeof(PagedResult<LeaveDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<LeaveDto>> List([FromQuery] LeaveQuery query, CancellationToken ct) => leaves.ListAllAsync(query, ct);

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(LeaveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<LeaveDto> Get(int id, CancellationToken ct) => leaves.GetAsync(id, ct);

    /// <summary>Approve or reject. Requires the version the reviewer saw (optimistic concurrency).</summary>
    [HttpPut("{id:int}/review")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType(typeof(LeaveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<LeaveDto> Review(int id, ReviewLeaveRequest request, CancellationToken ct) => leaves.ReviewAsync(id, request, ct);
}
