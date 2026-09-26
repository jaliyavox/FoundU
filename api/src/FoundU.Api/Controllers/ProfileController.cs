using FoundU.Api.Extensions;
using FoundU.Application.Abstractions;
using FoundU.Application.Auth.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoundU.Api.Controllers;

/// <summary>
/// Your own account. Name, email, student number and password - never role, and never
/// somebody else's account: every action reads the caller's id from the token.
/// </summary>
[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profile;

    public ProfileController(IProfileService profile)
    {
        _profile = profile;
    }

    [HttpGet]
    public async Task<ActionResult<ProfileDto>> Mine(CancellationToken cancellationToken)
        => Ok(await _profile.GetAsync(User.GetUserId(), cancellationToken));

    [HttpPut]
    public async Task<ActionResult<ProfileDto>> Update(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
        => Ok(await _profile.UpdateAsync(User.GetUserId(), request, cancellationToken));

    /// <summary>
    /// Returns a fresh token pair: changing the password signs out every other session, and
    /// this one would go with them otherwise.
    /// </summary>
    [HttpPost("password")]
    public async Task<ActionResult<AuthResponse>> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
        => Ok(await _profile.ChangePasswordAsync(
            User.GetUserId(),
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken));
}
