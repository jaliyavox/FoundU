using FoundU.Domain.Common;
using FoundU.Domain.Enums;

namespace FoundU.Domain.Entities;

/// <summary>
/// A signed-in user pressing "I found this" on someone's lost report.
///
/// Separate from <see cref="LostReportMessage"/> on purpose: a message is optional and says
/// where the item went, while this is the bare signal that somebody believes they have the
/// item. The author needs to know that the moment it happens, not only if the finder also
/// writes something.
///
/// It is not a claim of ownership and it moves no money and no item - the handover still
/// goes through a desk, which is what verifies the person collecting it is the owner.
/// </summary>
public class LostReportFoundClaim : BaseEntity
{
    public Guid LostReportId { get; set; }
    public LostReport LostReport { get; set; } = default!;

    /// <summary>The signed-in user who pressed it. Never the report's own author.</summary>
    public Guid FinderId { get; set; }
    public AppUser Finder { get; set; } = default!;

    /// <summary>Cleared when the author has seen it, so the card can stop flagging it.</summary>
    public bool IsSeenByOwner { get; set; }
    public DateTime? SeenAt { get; set; }

    /* ------------------------------------------------------------------ handover */

    public HandoverStatus Status { get; set; } = HandoverStatus.Declared;

    /// <summary>
    /// The six digits both sides quote at the desk, minted when the finder chooses to hand
    /// the item in - not at report time, and never on the public feed. Only this finder and
    /// the report's owner ever see it, and it stops working the moment the item is collected.
    /// </summary>
    public string? HandoverCode { get; set; }

    public DateTime? HandoverStartedAt { get; set; }

    /// <summary>
    /// When the pause lapses. A finder who says they are coming and never does must not keep
    /// a report off the feed for ever, so the code dies and the notice comes back.
    /// </summary>
    public DateTime? HandoverExpiresAt { get; set; }

    /// <summary>Set when a desk takes physical custody.</summary>
    public DateTime? HandedInAt { get; set; }
    public Guid? ReceivedByStaffId { get; set; }
    public AppUser? ReceivedByStaff { get; set; }

    /// <summary>Set when the owner walks away with it.</summary>
    public DateTime? CollectedAt { get; set; }
    public Guid? CollectedByStaffId { get; set; }
    public AppUser? CollectedByStaff { get; set; }

    /// <summary>
    /// What the desk checked before releasing the item - "Student ID, name matched". Recorded
    /// because a code alone is not proof of ownership, and a dispute needs a trail.
    /// </summary>
    public string? CollectionCheck { get; set; }

    /// <summary>The item once a desk has logged it, so the two records point at each other.</summary>
    public Guid? FoundReportId { get; set; }
    public FoundReport? FoundReport { get; set; }

    public bool IsHandoverOpen => Status is HandoverStatus.AwaitingHandIn or HandoverStatus.InCustody;
}
