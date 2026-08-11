using FoundU.Domain.Common;

namespace FoundU.Domain.Entities;

public class FoundItemPhoto : BaseEntity, ISoftDeletable
{
    public Guid FoundReportId { get; set; }
    public FoundReport FoundReport { get; set; } = default!;

    public string Url { get; set; } = default!;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
