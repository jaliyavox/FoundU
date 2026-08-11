using FoundU.Domain.Common;
using FoundU.Domain.Enums;

namespace FoundU.Domain.Entities;

public class LostReportStatusHistory : BaseEntity
{
    public Guid LostReportId { get; set; }
    public LostReport LostReport { get; set; } = default!;

    public LostReportStatus FromStatus { get; set; }
    public LostReportStatus ToStatus { get; set; }

    public Guid? ChangedByUserId { get; set; }
    public AppUser? ChangedByUser { get; set; }

    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
