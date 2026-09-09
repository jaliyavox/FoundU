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
    MessageReceived
}
