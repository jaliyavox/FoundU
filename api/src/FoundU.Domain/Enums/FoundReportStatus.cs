namespace FoundU.Domain.Enums;

public enum FoundReportStatus
{
    /// <summary>
    /// Posted by the student who found it, not yet at a desk. On the feed as a teaser so the
    /// owner can spot it; cannot be claimed until a desk confirms it and adds the hidden
    /// detail that verification rests on.
    /// </summary>
    Posted,

    /// <summary>At a desk, in storage, waiting for its owner.</summary>
    Unclaimed,

    /// <summary>A claim was approved; the owner holds a collection code.</summary>
    Claimed,

    Returned,
    Disposed
}
