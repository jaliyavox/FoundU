namespace FoundU.Domain.Enums;

public enum SupportTicketStatus
{
    /// <summary>Waiting for someone on the desk to answer.</summary>
    Open,

    /// <summary>Answered, and waiting on the person who asked.</summary>
    Waiting,

    /// <summary>Settled by staff. The person who asked can reopen it by writing again.</summary>
    Resolved,

    /// <summary>Closed for good - no more replies.</summary>
    Closed
}
