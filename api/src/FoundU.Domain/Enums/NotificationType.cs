namespace FoundU.Domain.Enums;

public enum NotificationType
{
    PossibleMatchFound,
    VerificationQuestionAvailable,
    RevisionRequested,
    ClaimApproved,
    ClaimRejected,
    CollectionInstructions,

    /// <summary>Someone pressed "I found this" on a lost report.</summary>
    ItemReportedFound,

    /// <summary>A finder wrote to the report's author.</summary>
    MessageReceived,

    /// <summary>The desk confirmed a finder's post - the item is in storage now.</summary>
    FoundPostConfirmed,

    /// <summary>An owner recognised the item a finder posted - time to walk it to a desk.</summary>
    FoundPostRecognised,

    /// <summary>An item a finder helped with reached its owner.</summary>
    ItemReturnedToOwner,

    /// <summary>The desk answered a support ticket.</summary>
    SupportTicketReply,

    /// <summary>A support ticket was resolved or closed.</summary>
    SupportTicketUpdated,

    /// <summary>A finder is walking the item to a desk - carries the code the owner collects with.</summary>
    HandoverStarted,

    /// <summary>The finder changed their mind before handing it in.</summary>
    HandoverCancelled
}
