using FoundU.Application.Matching.Dtos;

namespace FoundU.Application.Abstractions;

/// <summary>
/// Server-to-server boundary for the recommendation-only Matching Agent. Implementations return
/// only a bounded comparison result; they never create claims or change report status.
/// </summary>
public interface IMatchingAgentClient
{
    Task<MatchingAgentCallResult<MatchingAgentRecommendation>> MatchReportsAsync(
        MatchingAgentReportSummary lostReport,
        MatchingAgentReportSummary foundReport,
        string correlationId,
        CancellationToken cancellationToken = default);
}
