using FoundU.Domain.Common;

namespace FoundU.Domain.Entities;

public class LostItemPhoto : BaseEntity, ISoftDeletable
{
    public Guid LostReportId { get; set; }
    public LostReport LostReport { get; set; } = default!;

    public string Url { get; set; } = default!;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
