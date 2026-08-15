using FoundU.Domain.Common;
using FoundU.Domain.Enums;

namespace FoundU.Domain.Entities;

/// <summary>A human decision/revision on a Claim. One Claim can have many ApprovalDecisions to keep audit history of decisions and revisions.</summary>
public class ApprovalDecision : BaseEntity
{
    public Guid ClaimId { get; set; }
    public Claim Claim { get; set; } = default!;

    public Guid DecidedByUserId { get; set; }
    public AppUser DecidedByUser { get; set; } = default!;

    public ApprovalDecisionType Decision { get; set; }
    public string? Reason { get; set; }

    public bool IsOverride { get; set; }
    public Guid? OverriddenByUserId { get; set; }
    public AppUser? OverriddenByUser { get; set; }

    public DateTime DecidedAt { get; set; } = DateTime.UtcNow;
}
