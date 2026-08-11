using FoundU.Domain.Common;

namespace FoundU.Domain.Entities;

public class Category : BaseEntity, ISoftDeletable
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<LostReport> LostReports { get; set; } = new List<LostReport>();
    public ICollection<FoundReport> FoundReports { get; set; } = new List<FoundReport>();
}
