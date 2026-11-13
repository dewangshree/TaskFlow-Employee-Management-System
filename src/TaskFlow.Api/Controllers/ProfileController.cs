using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskFlow.Api.Configuration;
using TaskFlow.Application.Features.Profile;

namespace TaskFlow.Api.Controllers;

public sealed class ProfileController(IProfileService profile) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    public Task<ProfileDto> Get(CancellationToken ct) => profile.GetAsync(ct);

    [HttpPut]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<ProfileDto> Update(UpdateProfileRequest request, CancellationToken ct) => profile.UpdateAsync(request, ct);

    [HttpPut("password")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await profile.ChangePasswordAsync(request, ct);
        return NoContent();
    }
}
