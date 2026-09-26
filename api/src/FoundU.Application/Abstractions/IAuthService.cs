using FoundU.Application.Auth.Dtos;
using FoundU.Domain.Entities;

namespace FoundU.Application.Abstractions;

/// <summary>Orchestrates registration, login, refresh-token rotation and logout. Implemented in FoundU.Infrastructure (uses UserManager/SignInManager).</summary>
public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ipAddress);
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress);
    Task<AuthResponse> RefreshAsync(string refreshToken, string? ipAddress);

    /// <summary>Signs in with a verified Google ID token, linking or creating the account.</summary>
    Task<AuthResponse> GoogleSignInAsync(GoogleSignInRequest request, string? ipAddress);

    /// <summary>Issues a fresh token pair for an account the caller has already authenticated.</summary>
    Task<AuthResponse> IssueTokensForAsync(AppUser user, string? ipAddress);
    Task RevokeAsync(string refreshToken, string? ipAddress);
}
