using FoundU.Domain.Common;

namespace FoundU.Domain.Entities;

public class CampusLocation : BaseEntity, ISoftDeletable
{
    public string Name { get; set; } = default!;
    public string? Building { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<LostReport> LostReportsLastSeenHere { get; set; } = new List<LostReport>();
    public ICollection<FoundReport> FoundReportsFoundHere { get; set; } = new List<FoundReport>();
}
