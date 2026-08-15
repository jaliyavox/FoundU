using FoundU.Domain.Common;

namespace FoundU.Domain.Entities;

public class StorageLocation : BaseEntity, ISoftDeletable
{
    public string Name { get; set; } = default!;
    public string? Building { get; set; }
    public int? Capacity { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public ICollection<FoundReport> FoundReports { get; set; } = new List<FoundReport>();
    public ICollection<StorageTransfer> TransfersFrom { get; set; } = new List<StorageTransfer>();
    public ICollection<StorageTransfer> TransfersTo { get; set; } = new List<StorageTransfer>();
}
