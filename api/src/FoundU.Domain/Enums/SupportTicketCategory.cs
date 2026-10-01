namespace FoundU.Domain.Enums;

/// <summary>
/// What the ticket is about. Kept short on purpose: a long list makes people choose wrongly
/// and tells the desk less than the first sentence of the message does.
/// </summary>
public enum SupportTicketCategory
{
    Account,
    LostReport,
    FoundItem,
    Claim,
    Collection,
    Technical,
    Other
}
