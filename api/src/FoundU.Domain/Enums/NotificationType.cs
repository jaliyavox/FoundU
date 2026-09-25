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
    FoundPostRecognised
}
