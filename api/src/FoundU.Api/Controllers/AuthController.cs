using FoundU.Application.Abstractions;
using FoundU.Application.Auth.Dtos;
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

    public AuthController(IAuthService authService)
    {
        _authService = authService;
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
