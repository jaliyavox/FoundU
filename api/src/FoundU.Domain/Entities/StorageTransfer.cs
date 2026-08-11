using FoundU.Domain.Common;

namespace FoundU.Domain.Entities;

/// <summary>Immutable audit record of every physical movement of a found item between storage locations.</summary>
public class StorageTransfer : BaseEntity
{
    public Guid FoundReportId { get; set; }
    public FoundReport FoundReport { get; set; } = default!;

    public Guid? FromStorageLocationId { get; set; }
    public StorageLocation? FromStorageLocation { get; set; }

    public Guid ToStorageLocationId { get; set; }
    public StorageLocation ToStorageLocation { get; set; } = default!;

    public Guid TransferredByUserId { get; set; }
    public AppUser TransferredByUser { get; set; } = default!;

    public string? Reason { get; set; }
    public DateTime TransferredAt { get; set; } = DateTime.UtcNow;
}
