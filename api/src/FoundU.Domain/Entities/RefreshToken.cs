using FoundU.Domain.Common;

namespace FoundU.Domain.Entities;

/// <summary>
/// A rotatable refresh token. The raw token value is only ever returned to the client once, at
/// issuance - only its SHA-256 hash is stored, so a leaked database dump can't be replayed as a
/// valid token. Rotation-on-use: redeeming a refresh token revokes it and issues a new one,
/// recorded via ReplacedByTokenHash - this lets a reused/stolen token be detected and the whole
/// chain revoked (see AuthService.RefreshAsync).
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = default!;

    public string TokenHash { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }

    public string? CreatedByIp { get; set; }

    public DateTime? RevokedAt { get; set; }
    public string? RevokedByIp { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? ReasonRevoked { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt != null;
    public bool IsActive => !IsRevoked && !IsExpired;
}
