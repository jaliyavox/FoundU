using FoundU.Domain.Common;
using FoundU.Domain.Enums;

namespace FoundU.Domain.Entities;

public class ClaimStatusHistory : BaseEntity
{
    public Guid ClaimId { get; set; }
    public Claim Claim { get; set; } = default!;

    public ClaimStatus FromStatus { get; set; }
    public ClaimStatus ToStatus { get; set; }

    public Guid? ChangedByUserId { get; set; }
    public AppUser? ChangedByUser { get; set; }

    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
