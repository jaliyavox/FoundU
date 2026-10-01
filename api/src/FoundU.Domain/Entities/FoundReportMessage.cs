using FoundU.Domain.Common;

namespace FoundU.Domain.Entities;

/// <summary>
/// A message about something a student found and posted - the mirror of
/// <see cref="LostReportMessage"/>, with the roles the other way round.
///
/// Here the finder is the one being written to: somebody who thinks the item is theirs asks
/// about it, and the finder answers. That question needs no lost report behind it, because
/// asking "is the strap frayed?" is not a claim of ownership and moves nothing. The desk
/// still decides who the item goes home with.
///
/// One thread per enquirer, addressed by <see cref="RecipientId"/>, so two people asking
/// about the same item never read each other.
/// </summary>
public class FoundReportMessage : BaseEntity
{
    public Guid FoundReportId { get; set; }
    public FoundReport FoundReport { get; set; } = default!;

    public Guid SenderId { get; set; }
    public AppUser Sender { get; set; } = default!;

    /// <summary>Who this message is addressed to - the finder, or the enquirer being replied to.</summary>
    public Guid RecipientId { get; set; }
    public AppUser Recipient { get; set; } = default!;

    public string Body { get; set; } = default!;

    public bool IsRead { get; set; }
}
