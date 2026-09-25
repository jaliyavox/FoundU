using FoundU.Domain.Common;

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
}
