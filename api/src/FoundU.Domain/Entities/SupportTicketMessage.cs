using FoundU.Domain.Common;

namespace FoundU.Domain.Entities;

/// <summary>
/// One message on a support ticket. Unlike the item threads there is no recipient: a ticket
/// is one conversation between the person who opened it and whoever is on the desk, and any
/// staff member may pick it up mid-way.
/// </summary>
public class SupportTicketMessage : BaseEntity
{
    public Guid SupportTicketId { get; set; }
    public SupportTicket SupportTicket { get; set; } = default!;

    public Guid SenderId { get; set; }
    public AppUser Sender { get; set; } = default!;

    /// <summary>
    /// Recorded rather than derived from the sender's role: a staff member who opens a ticket
    /// of their own is writing as themselves, and roles change later.
    /// </summary>
    public bool IsStaffReply { get; set; }

    public string Body { get; set; } = default!;

    /// <summary>Whether the other side has seen it - drives the unread marks on both queues.</summary>
    public bool IsRead { get; set; }
}
