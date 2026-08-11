using FoundU.Domain.Common;
using FoundU.Domain.Enums;

namespace FoundU.Domain.Entities;

public class FoundReport : BaseEntity, ISoftDeletable
{
    public Guid StaffId { get; set; }
    public AppUser Staff { get; set; } = default!;

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = default!;

    public Guid FoundLocationId { get; set; }
    public CampusLocation FoundLocation { get; set; } = default!;

    public Guid StorageLocationId { get; set; }
    public StorageLocation StorageLocation { get; set; } = default!;

    /// <summary>Visible to students browsing found items.</summary>
    public string PublicDescription { get; set; } = default!;

    /// <summary>
    /// NEVER exposed to Student DTOs. Used only by the Verification Agent and Staff/Admin
    /// to construct ownership-verification questions.
    /// </summary>
    public string PrivateVerificationDetails { get; set; } = default!;

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
}
