using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskFlow.Api.Configuration;
using TaskFlow.Api.Security;
using TaskFlow.Application.Common;
using TaskFlow.Application.Features.Employees;

namespace TaskFlow.Api.Controllers;

[Authorize(Policy = Policies.AdminOnly)]
public sealed class EmployeesController(IEmployeeService employees, IEmployeeDirectory directory) : ApiControllerBase
{
    [HttpGet]
    [EnableRateLimiting(RateLimitPolicies.Search)]
    [ProducesResponseType(typeof(PagedResult<EmployeeDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<EmployeeDto>> List([FromQuery] EmployeeQuery query, CancellationToken ct) => employees.ListAsync(query, ct);

    /// <summary>Activate or deactivate an employee (idempotent).</summary>
    [HttpPut("{id}/active")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    public Task<EmployeeDto> SetActive(string id, SetEmployeeActiveRequest request, CancellationToken ct) =>
        employees.SetActiveAsync(id, request, ct);

    /// <summary>Active employees for assignment dropdowns (cached).</summary>
    [HttpGet("assignable")]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeLookupDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<EmployeeLookupDto>> Assignable(CancellationToken ct) => directory.GetAssignableAsync(ct);

    /// <summary>Distinct departments for filters (cached).</summary>
    [HttpGet("departments")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<string>> Departments(CancellationToken ct) => directory.GetDepartmentsAsync(ct);
}
