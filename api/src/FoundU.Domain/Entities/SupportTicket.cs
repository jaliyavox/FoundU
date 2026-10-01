using FoundU.Domain.Common;
using FoundU.Domain.Enums;

namespace FoundU.Domain.Entities;

/// <summary>
/// Someone asking the people who run FoundU for help.
///
/// Separate from the report and claim threads on purpose: those are conversations between
/// two students about one item, while this is a conversation with the institution, and it
/// has to survive the item being resolved, withdrawn or never found at all.
///
/// A ticket may name what it is about - a report, a claim - so the desk can open the right
/// record without asking. Nothing is inferred from that link: it is a pointer, not a grant.
/// </summary>
public class SupportTicket : BaseEntity, ISoftDeletable
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = default!;

    public string Subject { get; set; } = default!;
    public SupportTicketCategory Category { get; set; }
    public SupportTicketStatus Status { get; set; } = SupportTicketStatus.Open;

    /// <summary>The staff member who picked it up, if anyone has.</summary>
    public Guid? AssignedToUserId { get; set; }
    public AppUser? AssignedToUser { get; set; }

    /// <summary>What the ticket is about, when the person said - "LostReport", "Claim", and so on.</summary>
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }

    /// <summary>Sorting a queue by "who has been waiting longest" needs the last word, not the first.</summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }

    public ICollection<SupportTicketMessage> Messages { get; set; } = new List<SupportTicketMessage>();

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
