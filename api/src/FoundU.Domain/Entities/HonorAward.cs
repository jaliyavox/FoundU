using FoundU.Domain.Common;
using FoundU.Domain.Enums;

namespace FoundU.Domain.Entities;

/// <summary>
/// One line in a person's honor ledger - points, why, and what they were for.
///
/// A ledger rather than a running total on the user: the "Help to find" page has to show what
/// each award was for, and a total that cannot be traced back to real events is a total nobody
/// can argue with when it looks wrong.
///
/// Awards are made by the services that see the outcome (a desk confirming custody, an owner
/// closing their report, a claim being collected), never by the person earning them.
/// </summary>
public class HonorAward : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = default!;

    public HonorAwardReason Reason { get; set; }
    public int Points { get; set; }

    /// <summary>The report they helped close, when the award came from someone's lost report.</summary>
    public Guid? LostReportId { get; set; }
    public LostReport? LostReport { get; set; }

    /// <summary>The item in custody, when the award came from a hand-in.</summary>
    public Guid? FoundReportId { get; set; }
    public FoundReport? FoundReport { get; set; }

    /// <summary>One line for the activity list, written when the award is made.</summary>
    public string Detail { get; set; } = default!;
}
