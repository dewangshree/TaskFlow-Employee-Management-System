using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using TaskFlow.Api.Configuration;
using TaskFlow.Api.Security;
using TaskFlow.Application.Common;
using TaskFlow.Application.Features.Auth;
using TaskFlow.Application.Features.Employees;
using TaskFlow.Domain.Common;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Api.Controllers;

public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IEmployeeDirectory directory,
    ILogger<AuthController> logger) : ApiControllerBase
{
    /// <summary>Issues the XSRF-TOKEN cookie the SPA must echo on state-changing requests.</summary>
    [HttpGet("csrf")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Csrf()
    {
        CsrfProtection.IssueToken(HttpContext);
        return NoContent();
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(typeof(CurrentUserDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
        {
            logger.LogInformation("Login rejected for unknown or inactive account");
            return InvalidCredentials();
        }

        var result = await signInManager.PasswordSignInAsync(user, request.Password, request.RememberMe, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            logger.LogWarning("Account {UserId} locked out after repeated failed logins", user.Id);
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Account locked",
                detail: "Too many failed attempts. Try again in a few minutes.");
        }

        if (!result.Succeeded)
            return InvalidCredentials();

        return Ok(await SignedInResponseAsync(user));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(typeof(CurrentUserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Department = request.Department.Trim(),
            JoinDate = DateTime.UtcNow,
            EmailConfirmed = true,
            IsActive = true,
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            // Map Identity errors onto the form fields they belong to.
            var errors = result.Errors
                .GroupBy(e => e.Code.Contains("Password", StringComparison.Ordinal) ? "password" : "email")
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
            throw new RequestValidationException(errors);
        }

        // Self-registration always creates an Employee (unchanged business rule).
        await userManager.AddToRoleAsync(user, RoleNames.Employee);
        await signInManager.SignInAsync(user, isPersistent: false);
        await directory.InvalidateAsync();

        return StatusCode(StatusCodes.Status201Created, await SignedInResponseAsync(user));
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        CsrfProtection.IssueToken(HttpContext); // token for the now-anonymous session
        return NoContent();
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null || !user.IsActive)
            return Unauthorized();

        return Ok(await ToDtoAsync(user));
    }

    private async Task<CurrentUserDto> SignedInResponseAsync(ApplicationUser user)
    {
        // Antiforgery tokens are bound to the user identity, so a new token must be
        // issued for the freshly signed-in principal within this same response.
        HttpContext.User = await signInManager.CreateUserPrincipalAsync(user);
        CsrfProtection.IssueToken(HttpContext);
        return await ToDtoAsync(user);
    }

    private async Task<CurrentUserDto> ToDtoAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new CurrentUserDto(user.Id, user.Email ?? string.Empty, user.FirstName, user.LastName, user.FullName,
            user.Department, roles.Contains(RoleNames.Admin) ? RoleNames.Admin : RoleNames.Employee);
    }

    private ObjectResult InvalidCredentials() =>
        Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials", detail: "Invalid email or password.");
}
