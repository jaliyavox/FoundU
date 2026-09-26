namespace FoundU.Application.LostReports.Dtos;

/// <summary>Strict, safe description attributes returned by the parser after client validation.</summary>
public record DescriptionParserAgentResult(
    string ItemType,
    string PrimaryColor,
    string? SecondaryColor,
    IReadOnlyList<string> IdentifyingFeatures,
    decimal ConfidenceScore,
    string AgentRunId);

/// <summary>Generic failure that never contains a description, raw provider response, or exception body.</summary>
public record DescriptionParserAgentCallResult<T>(bool IsSuccess, T? Value, string? FailureReason, int RetryCount = 0)
{
    public static DescriptionParserAgentCallResult<T> Success(T value, int retryCount = 0) => new(true, value, null, retryCount);

    public static DescriptionParserAgentCallResult<T> Failure(string reason, int retryCount = 0) => new(false, default, reason, retryCount);
}
