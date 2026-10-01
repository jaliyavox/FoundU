using FoundU.Application.Auth.Dtos;

namespace FoundU.Application.Abstractions;

public interface IProfileService
{
    Task<ProfileDto> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ProfileDto> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes (or first sets) the password and returns fresh tokens, because every other
    /// session is signed out - a password change is how someone takes an account back.
    /// </summary>
    Task<AuthResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, string? ipAddress, CancellationToken cancellationToken = default);
}
