using FoundU.Domain.Common;

namespace FoundU.Domain.Entities;

public class AuditLog : BaseEntity
{
    /// <summary>Null for system-generated events with no acting user.</summary>
    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }

    public string Action { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public Guid? EntityId { get; set; }
    public string? DetailsJson { get; set; }
    public string? IpAddress { get; set; }
}
