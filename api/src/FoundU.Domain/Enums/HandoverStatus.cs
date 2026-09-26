namespace FoundU.Domain.Enums;

/// <summary>
/// Where a finder's handover has got to. A found-claim starts as <see cref="Declared"/> -
/// "I have this" - and only becomes a handover when the finder chooses to walk it to a desk.
/// </summary>
public enum HandoverStatus
{
    /// <summary>They pressed "I found this" and nothing more. No code, nothing paused.</summary>
    Declared,

    /// <summary>They have a code and are taking it to a desk. The report is paused.</summary>
    AwaitingHandIn,

    /// <summary>A desk has the item. The owner collects it with the same code.</summary>
    InCustody,

    /// <summary>The owner collected it.</summary>
    Collected,

    /// <summary>Nobody turned up in time. The report goes back on the feed.</summary>
    Expired,

    /// <summary>The finder changed their mind before handing it in.</summary>
    Cancelled
}
