using FoundU.Domain.Entities;

namespace FoundU.Application.Abstractions;

public record AccessToken(string Value, DateTime ExpiresAtUtc);

/// <summary>Pure JWT generation - no persistence, no HTTP concerns. Implemented in FoundU.Infrastructure.</summary>
public interface ITokenService
{
    /// <summary>Builds a short-lived signed JWT carrying sub/email/name/role claims for this user.</summary>
    AccessToken GenerateAccessToken(AppUser user);

    /// <summary>Generates a cryptographically random opaque refresh token string (not a JWT).</summary>
    string GenerateRefreshTokenValue();

    /// <summary>One-way hash used to store refresh tokens at rest - never store the raw value.</summary>
    string HashToken(string rawToken);
}
