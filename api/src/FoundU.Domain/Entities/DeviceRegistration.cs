using FoundU.Domain.Common;

namespace FoundU.Domain.Entities;

/// <summary>
/// A mobile device token owned by a user. Tokens are server-side delivery credentials and are
/// intentionally never included in API responses.
/// </summary>
public class DeviceRegistration : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = default!;

    public string FcmToken { get; set; } = default!;
    public string Platform { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public DateTime? DeactivatedAt { get; set; }
}
