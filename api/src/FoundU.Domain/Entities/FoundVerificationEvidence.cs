using FoundU.Domain.Common;
namespace FoundU.Domain.Entities;

/// <summary>Additional physical evidence recorded by staff. Never projected to students.</summary>
public class FoundVerificationEvidence : BaseEntity
{
    public Guid FoundReportId { get; set; }
    public FoundReport FoundReport { get; set; } = default!;
    public Guid RecordedByUserId { get; set; }
    public AppUser RecordedByUser { get; set; } = default!;
    public string Detail { get; set; } = default!;
}
