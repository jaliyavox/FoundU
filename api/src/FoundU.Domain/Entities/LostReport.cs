using FoundU.Domain.Common;
using FoundU.Domain.Enums;

namespace FoundU.Domain.Entities;

public class LostReport : BaseEntity, ISoftDeletable
{
    public Guid StudentId { get; set; }
    public AppUser Student { get; set; } = default!;

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = default!;

    public Guid ItemTypeId { get; set; }
    public ItemType ItemType { get; set; } = default!;

    public Guid LastSeenLocationId { get; set; }
    public CampusLocation LastSeenLocation { get; set; } = default!;

    public string Description { get; set; } = default!;
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }

    /// <summary>Raw identifying features as entered by the student (e.g. "small keychain").</summary>
    public string? IdentifyingFeaturesJson { get; set; }

    /// <summary>Structured JSON output produced by the Description-Parsing Agent.</summary>
    public string? ParsedAttributesJson { get; set; }

    /// <summary>
    /// Approximate window the student believes the item was lost, e.g. 2:00 PM - 3:30 PM.
    /// Replaces the previous single LastSeenAt instant - the Matching Agent compares a
    /// Found Item's FoundAt timestamp against this range instead of an exact moment.
    /// EstimatedLostFromAt must be &lt;= EstimatedLostToAt (enforced by a DB check constraint).
    /// </summary>
    public DateTime EstimatedLostFromAt { get; set; }
    public DateTime EstimatedLostToAt { get; set; }

    public LostReportStatus Status { get; set; } = LostReportStatus.Active;

    /// <summary>
    /// Six digits a finder quotes at the desk so staff can link the item to this report
    /// without searching. Unique across reports; visible on the public feed because it
    /// routes an item, it does not prove ownership.
    /// </summary>
    public string HandInCode { get; set; } = HandoverCodes.Generate();

    public string? WithdrawReason { get; set; }
    public DateTime? WithdrawnAt { get; set; }

    public bool IsFlagged { get; set; }
    public string? FlagReason { get; set; }
    public DateTime? FlaggedAt { get; set; }

    /// <summary>
    /// Who raised the flag. Staff working the moderation queue read a flag differently
    /// depending on whether the owner raised it on their own report or a staff member did.
    /// </summary>
    public Guid? FlaggedByUserId { get; set; }
    public AppUser? FlaggedByUser { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public ICollection<LostItemPhoto> Photos { get; set; } = new List<LostItemPhoto>();
    public ICollection<LostReportStatusHistory> StatusHistory { get; set; } = new List<LostReportStatusHistory>();
    public ICollection<MatchSuggestion> MatchSuggestions { get; set; } = new List<MatchSuggestion>();
    public ICollection<Claim> Claims { get; set; } = new List<Claim>();
    public ICollection<LostReportMessage> Messages { get; set; } = new List<LostReportMessage>();
    public ICollection<LostReportFoundClaim> FoundClaims { get; set; } = new List<LostReportFoundClaim>();
}
