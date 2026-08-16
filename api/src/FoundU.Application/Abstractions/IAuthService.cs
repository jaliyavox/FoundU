using FoundU.Application.Auth.Dtos;

namespace FoundU.Application.Abstractions;

/// <summary>Orchestrates registration, login, refresh-token rotation and logout. Implemented in FoundU.Infrastructure (uses UserManager/SignInManager).</summary>
public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ipAddress);
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress);
    Task<AuthResponse> RefreshAsync(string refreshToken, string? ipAddress);
    Task RevokeAsync(string refreshToken, string? ipAddress);
}
