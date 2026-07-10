using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Helpers;
using TaskFlow.Models;
using TaskFlow.Services;

namespace TaskFlow.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly DashboardService _dashboardService;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardController(DashboardService dashboardService, UserManager<ApplicationUser> userManager)
    {
        _dashboardService = dashboardService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        if (User.IsInRole(RoleNames.Admin))
        {
            var model = await _dashboardService.GetAdminDashboardAsync();
            model.UserName = User.Identity?.Name ?? "Admin";
            return View("Admin", model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction("Login", "Account");

        var employeeModel = await _dashboardService.GetEmployeeDashboardAsync(user.Id, user.FullName);
        return View("Employee", employeeModel);
    }
}
