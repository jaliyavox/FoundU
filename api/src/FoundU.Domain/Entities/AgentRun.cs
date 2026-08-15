using FoundU.Domain.Common;
using FoundU.Domain.Enums;

namespace FoundU.Domain.Entities;

/// <summary>
/// Records one execution of the Agentic AI workflow (Planner -> Description-Parsing / Matching ->
/// Verification -> deterministic validation -> human approval).
/// </summary>
public class AgentRun : BaseEntity
{
    /// <summary>Set when the run is evaluating a specific Claim (the main assessed workflow).</summary>
    public Guid? ClaimId { get; set; }
    public Claim? Claim { get; set; }

    /// <summary>Polymorphic trigger reference, e.g. "LostReport" + id for description parsing on creation.</summary>
    public string TriggerEntityType { get; set; } = default!;
    public Guid TriggerEntityId { get; set; }

    public string Objective { get; set; } = default!;

    /// <summary>Structured plan JSON produced by the Planner/Coordinator Agent.</summary>
    public string? PlanJson { get; set; }

    public AgentRunStatus Status { get; set; } = AgentRunStatus.Running;

    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>Final auditable outcome summary (never raw model reasoning/chain-of-thought).</summary>
    public string? FinalOutcomeJson { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public ICollection<AgentStep> Steps { get; set; } = new List<AgentStep>();
    public ICollection<MatchSuggestion> MatchSuggestions { get; set; } = new List<MatchSuggestion>();
    public ICollection<VerificationQuestion> VerificationQuestions { get; set; } = new List<VerificationQuestion>();
}
