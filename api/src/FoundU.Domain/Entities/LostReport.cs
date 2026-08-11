using FoundU.Domain.Common;
using FoundU.Domain.Enums;

namespace FoundU.Domain.Entities;

public class LostReport : BaseEntity, ISoftDeletable
{
    public Guid StudentId { get; set; }
    public AppUser Student { get; set; } = default!;

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = default!;

    public Guid LastSeenLocationId { get; set; }
    public CampusLocation LastSeenLocation { get; set; } = default!;

    public string Description { get; set; } = default!;
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }

    /// <summary>Raw identifying features as entered by the student (e.g. "small keychain").</summary>
    public string? IdentifyingFeaturesJson { get; set; }

    /// <summary>Structured JSON output produced by the Description-Parsing Agent.</summary>
    public string? ParsedAttributesJson { get; set; }

    public DateTime LastSeenAt { get; set; }
    public LostReportStatus Status { get; set; } = LostReportStatus.Active;

    public string? WithdrawReason { get; set; }
    public DateTime? WithdrawnAt { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public ICollection<LostItemPhoto> Photos { get; set; } = new List<LostItemPhoto>();
    public ICollection<LostReportStatusHistory> StatusHistory { get; set; } = new List<LostReportStatusHistory>();
    public ICollection<MatchSuggestion> MatchSuggestions { get; set; } = new List<MatchSuggestion>();
    public ICollection<Claim> Claims { get; set; } = new List<Claim>();
}
