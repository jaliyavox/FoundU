using FoundU.Domain.Common;

namespace FoundU.Domain.Entities;

public class Category : BaseEntity, ISoftDeletable
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Surfaced first and visually emphasised in the report forms. Used for categories where
    /// losing the item is urgent - identity documents, for instance, where the owner needs to
    /// act quickly and the finder should hand it in rather than sit on it.
    /// </summary>
    public bool IsHighlighted { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<LostReport> LostReports { get; set; } = new List<LostReport>();
    public ICollection<FoundReport> FoundReports { get; set; } = new List<FoundReport>();
    public ICollection<ItemType> ItemTypes { get; set; } = new List<ItemType>();
}
