using FoundU.Domain.Common;
using FoundU.Domain.Enums;

namespace FoundU.Domain.Entities;

public class Claim : BaseEntity, ISoftDeletable
{
    public Guid StudentId { get; set; }
    public AppUser Student { get; set; } = default!;

    public Guid LostReportId { get; set; }
    public LostReport LostReport { get; set; } = default!;

    public Guid FoundReportId { get; set; }
    public FoundReport FoundReport { get; set; } = default!;

    public ClaimStatus Status { get; set; } = ClaimStatus.Pending;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public ICollection<VerificationQuestion> VerificationQuestions { get; set; } = new List<VerificationQuestion>();
    public ICollection<ClaimAnswer> Answers { get; set; } = new List<ClaimAnswer>();
    public ICollection<ClaimStatusHistory> StatusHistory { get; set; } = new List<ClaimStatusHistory>();
    public ICollection<AgentRun> AgentRuns { get; set; } = new List<AgentRun>();

    /// <summary>
    /// Full decision history for this claim (one-to-many, not one-to-one). A claim can be
    /// revised multiple times before a final outcome, e.g. RevisionRequested -> Approved -
    /// every decision is preserved for audit. The most recent row is the current decision.
    /// </summary>
    public ICollection<ApprovalDecision> ApprovalDecisions { get; set; } = new List<ApprovalDecision>();
}
