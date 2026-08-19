using FoundU.Domain.Common;

namespace FoundU.Domain.Entities;

/// <summary>
/// A message sent to the author of a lost report by another signed-in user - typically
/// someone who has found the item or seen it somewhere.
///
/// Deliberately in-app and one-directional. Neither side ever sees the other's email or
/// student number, and nothing here arranges a handover: the item still goes through a desk,
/// which is what verifies the person collecting it is the owner.
/// </summary>
public class LostReportMessage : BaseEntity
{
    public Guid LostReportId { get; set; }
    public LostReport LostReport { get; set; } = default!;

    /// <summary>The signed-in user who wrote it. Never the report's own author.</summary>
    public Guid SenderId { get; set; }
    public AppUser Sender { get; set; } = default!;

    public string Body { get; set; } = default!;

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}
