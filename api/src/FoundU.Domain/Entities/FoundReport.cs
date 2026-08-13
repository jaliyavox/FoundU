using FoundU.Domain.Common;
using FoundU.Domain.Enums;

namespace FoundU.Domain.Entities;

public class FoundReport : BaseEntity, ISoftDeletable
{
    public Guid StaffId { get; set; }
    public AppUser Staff { get; set; } = default!;

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = default!;

    public Guid ItemTypeId { get; set; }
    public ItemType ItemType { get; set; } = default!;

    public Guid FoundLocationId { get; set; }
    public CampusLocation FoundLocation { get; set; } = default!;

    public Guid StorageLocationId { get; set; }
    public StorageLocation StorageLocation { get; set; } = default!;

    /// <summary>
    /// Staff-entered general description used for matching and internal operations.
    /// (Renamed from PublicDescription - there is no student-facing public listing;
    /// only Staff/Security create Found Reports.)
    /// </summary>
    public string GeneralDescription { get; set; } = default!;

    /// <summary>
    /// Hidden ownership evidence used only by Staff/Admin and the Verification Agent
    /// (e.g. "Pink keychain in front pocket, Dell charger, GitHub sticker").
    /// NEVER exposed to Student DTOs.
    /// Nullable: some items genuinely have no useful unique detail (e.g. generic earbuds) -
    /// staff should not be forced to enter fake information. The Application/Service layer
    /// decides whether what's here is sufficient for automated verification; if not, the
    /// workflow routes the claim to manual review instead.
    /// </summary>
    public string? PrivateVerificationDetails { get; set; }

    /// <summary>Structured JSON of observable attributes (brand, colour, compartments, etc).</summary>
    public string? ObservedAttributesJson { get; set; }

    /// <summary>
    /// Structured JSON mirror of the private verification evidence (containsIdCard, cashAmount,
    /// keychainColour, etc) so the Verification Agent can compare fields instead of free text.
    /// NEVER exposed to Student DTOs.
    /// </summary>
    public string? PrivateVerificationAttributesJson { get; set; }

    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }

    public DateTime FoundAt { get; set; }
    public FoundReportStatus Status { get; set; } = FoundReportStatus.Unclaimed;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public ICollection<FoundItemPhoto> Photos { get; set; } = new List<FoundItemPhoto>();
    public ICollection<StorageTransfer> StorageTransfers { get; set; } = new List<StorageTransfer>();
    public ICollection<MatchSuggestion> MatchSuggestions { get; set; } = new List<MatchSuggestion>();
    public ICollection<Claim> Claims { get; set; } = new List<Claim>();
    public ICollection<FoundReportStatusHistory> StatusHistory { get; set; } = new List<FoundReportStatusHistory>();
}
