using FoundU.Application.FoundReports.Dtos;

namespace FoundU.Application.Matching.Dtos;

/// <summary>
/// Staff linking an item they have in storage to a student's lost report.
///
/// Staff can make this link directly. A separate staff-only AI-assisted endpoint may create the
/// same suggestion after a validated, recommendation-only comparison.
/// </summary>
public record CreateMatchSuggestionRequest(Guid LostReportId, Guid FoundReportId, string? Note);

/// <summary>Safe result of a staff-requested agent comparison. A suggestion exists only for a validated candidate.</summary>
public record GenerateMatchSuggestionResultDto(
    string Recommendation,
    decimal Score,
    MatchSuggestionDto? Suggestion);

/// <summary>
/// A candidate as the student sees it on their own report.
///
/// The item is projected through <see cref="FoundReportSummaryDto"/> - the student-safe shape,
/// with no hidden verification evidence. Seeing that an item exists is not the same as proving
/// it is yours, which is what the claim's questions are for.
/// </summary>
public record MatchSuggestionDto(
    Guid Id,
    Guid LostReportId,
    string LostReportDescription,
    FoundReportSummaryDto FoundItem,
    string Status,
    string? Note,
    /// <summary>
    /// False while a person made the link by hand. The score is only meaningful when the
    /// Matching Agent produced it, so the UI shows one only for agent-generated rows.
    /// </summary>
    bool IsAgentGenerated,
    decimal? MatchScore,
    /// <summary>Set once the student has opened a claim from this suggestion.</summary>
    Guid? ClaimId,
    DateTime CreatedAt);

public record DismissMatchSuggestionRequest(string? Reason);
