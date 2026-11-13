using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Security;
using TaskFlow.Application.Abstractions;
using TaskFlow.Application.Features.Dashboard;

namespace TaskFlow.Api.Controllers;

public sealed class DashboardController(IDashboardService dashboard, ICurrentUser currentUser) : ApiControllerBase
{
    [HttpGet("admin")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType(typeof(AdminDashboardDto), StatusCodes.Status200OK)]
    public async Task<AdminDashboardDto> Admin(CancellationToken ct) => await dashboard.GetAdminDashboardAsync(ct);

    [HttpGet("me")]
    [Authorize(Policy = Policies.EmployeeOnly)]
    [ProducesResponseType(typeof(EmployeeDashboardDto), StatusCodes.Status200OK)]
    public async Task<EmployeeDashboardDto> Mine(CancellationToken ct) =>
        await dashboard.GetEmployeeDashboardAsync(currentUser.RequiredUserId, ct);
}
