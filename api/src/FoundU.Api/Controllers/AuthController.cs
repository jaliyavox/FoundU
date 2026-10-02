using FoundU.Application.Abstractions;
using FoundU.Application.Auth.Dtos;
using FoundU.Application.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FoundU.Api.Controllers;

/// <summary>See /docs/api/conventions.md "Auth flow" for the full request/response contract.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IAccountEmailService _emails;

    public AuthController(IAuthService authService, IAccountEmailService emails)
    {
        _authService = authService;
        _emails = emails;
    }

    /// <summary>
    /// Emails a reset link if the address has an account. Always 202 with the same body, so
    /// this cannot be used to find out who has an account.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await _emails.RequestPasswordResetAsync(request.Email, cancellationToken);
        return Accepted(new { message = "If that address has a FoundU account, a reset link is on its way." });
    }

    /// <summary>From the emailed link. Sets the new password and signs every other session out.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await _emails.ResetPasswordAsync(request, GetClientIp(), cancellationToken);
        return NoContent();
    }

    [HttpPost("confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request, CancellationToken cancellationToken)
    {
        await _emails.ConfirmEmailAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>Sends the confirmation link again, to the signed-in account's current address.</summary>
    [HttpPost("resend-confirmation")]
    [Authorize]
    public async Task<IActionResult> ResendConfirmation(CancellationToken cancellationToken)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(id, out var userId)) return Unauthorized();
        await _emails.SendConfirmationAsync(userId, cancellationToken);
        return NoContent();
    }

    /// <summary>Student self-registration. Staff/Admin accounts are created by an Admin via a separate management endpoint.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request, GetClientIp());
        return Ok(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request, GetClientIp());
        return Ok(result);
    }

    /// <summary>Exchanges a still-valid refresh token for a new access + refresh token pair (rotation).</summary>
    /// <summary>
    /// Signs in with the ID token Google's button produced. Creates the account on first use,
    /// or links Google to an existing one when Google has verified the address.
    /// </summary>
    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> Google([FromBody] GoogleSignInRequest request)
        => Ok(await _authService.GoogleSignInAsync(request, GetClientIp()));

    /// <summary>Whether this server has Google sign-in configured, so clients can hide the button.</summary>
    [HttpGet("google/status")]
    [AllowAnonymous]
    public ActionResult<object> GoogleStatus([FromServices] IGoogleTokenVerifier verifier)
        => Ok(new { enabled = verifier.IsConfigured, clientId = verifier.ClientId });

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshTokenRequest request)
    {
        var result = await _authService.RefreshAsync(request.RefreshToken, GetClientIp());
        return Ok(result);
    }

    /// <summary>Revokes a refresh token (logout on this device). Idempotent - always returns 204.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] RevokeTokenRequest request)
    {
        await _authService.RevokeAsync(request.RefreshToken, GetClientIp());
        return NoContent();
    }

    /// <summary>Returns the currently authenticated user's profile, derived from the access token's claims.</summary>
    [HttpGet("me")]
    [Authorize]
    public ActionResult<object> Me()
    {
        return Ok(new
        {
            Id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"),
            Email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email"),
            Name = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name"),
            Role = User.FindFirstValue(ClaimTypes.Role)
        });
    }

    private string? GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
