using FoundU.Domain.Common;

namespace FoundU.Domain.Entities;

/// <summary>
/// Second-level classification under Category (e.g. Category "Bags & Wallets" -> ItemType
/// "Backpack" / "Laptop Bag" / "Wallet"). Lets forms and Matching Agent comparisons be more
/// precise than Category alone.
/// </summary>
public class ItemType : BaseEntity, ISoftDeletable
{
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = default!;

    public string Name { get; set; } = default!;
    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<LostReport> LostReports { get; set; } = new List<LostReport>();
    public ICollection<FoundReport> FoundReports { get; set; } = new List<FoundReport>();
}
