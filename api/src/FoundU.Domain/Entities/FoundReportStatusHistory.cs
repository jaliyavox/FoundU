using FoundU.Domain.Common;
using FoundU.Domain.Enums;

namespace FoundU.Domain.Entities;

public class FoundReportStatusHistory : BaseEntity
{
    public Guid FoundReportId { get; set; }
    public FoundReport FoundReport { get; set; } = default!;

    public FoundReportStatus FromStatus { get; set; }
    public FoundReportStatus ToStatus { get; set; }

    public Guid? ChangedByUserId { get; set; }
    public AppUser? ChangedByUser { get; set; }

    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
