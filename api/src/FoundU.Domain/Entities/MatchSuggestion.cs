using FoundU.Domain.Common;
using FoundU.Domain.Enums;

namespace FoundU.Domain.Entities;

/// <summary>Links one LostReport with one FoundReport and stores the AI Matching Agent's score/recommendation.</summary>
public class MatchSuggestion : BaseEntity
{
    public Guid LostReportId { get; set; }
    public LostReport LostReport { get; set; } = default!;

    public Guid FoundReportId { get; set; }
    public FoundReport FoundReport { get; set; } = default!;

    /// <summary>0.00 - 1.00 confidence score produced by the Matching Agent.</summary>
    public decimal MatchScore { get; set; }

    public string? MatchingFactorsJson { get; set; }
    public string? ConflictingFactorsJson { get; set; }

    public MatchSuggestionStatus Status { get; set; } = MatchSuggestionStatus.Suggested;

    public Guid? GeneratedByAgentRunId { get; set; }
    public AgentRun? GeneratedByAgentRun { get; set; }

    public ICollection<MatchStatusHistory> StatusHistory { get; set; } = new List<MatchStatusHistory>();
}
