using System.Text.Json.Serialization;

namespace FoundU.Application.Matching.Dtos;

/// <summary>Minimum safe report fields accepted by FastAPI's deterministic matcher.</summary>
public record MatchingAgentReportSummary(
    [property: JsonPropertyName("report_id")] string ReportId,
    [property: JsonPropertyName("item_type")] string ItemType,
    [property: JsonPropertyName("primary_color")] string PrimaryColor);

/// <summary>Validated, non-authoritative comparison result returned by the Matching Agent.</summary>
public record MatchingAgentRecommendation(string Recommendation, decimal Score, string AgentRunId);

/// <summary>
/// A generic, safe client failure. It never carries a raw FastAPI response, request payload, or
/// private verification data and can therefore be used in a bounded audit outcome.
/// </summary>
public record MatchingAgentCallResult<T>(bool IsSuccess, T? Value, string? FailureReason)
{
    public static MatchingAgentCallResult<T> Success(T value) => new(true, value, null);

    public static MatchingAgentCallResult<T> Failure(string reason) => new(false, default, reason);
}
