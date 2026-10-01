using System.Text.Json.Serialization;

namespace FoundU.Application.Claims.Dtos;

/// <summary>Safe question fields exchanged with the AI service; no expected answer is included.</summary>
public record VerificationAgentQuestion(
    [property: JsonPropertyName("question_id")] string QuestionId,
    string Question);

public record VerificationAgentAnswer(
    [property: JsonPropertyName("question_id")] string QuestionId,
    string Answer);

public record GenerateVerificationQuestionsResult(
    Guid ClaimId,
    IReadOnlyList<VerificationAgentQuestion> Questions,
    string Recommendation,
    string AgentRunId);

public record EvaluateVerificationAnswersResult(
    Guid ClaimId,
    string Recommendation,
    string AgentRunId);

/// <summary>
/// A deliberately generic failure result. It is safe to persist in audit metadata and never
/// contains the outgoing hidden evidence, student answer text, or AI response body.
/// </summary>
public record VerificationAgentCallResult<T>(bool IsSuccess, T? Value, string? FailureReason, int RetryCount = 0)
{
    public static VerificationAgentCallResult<T> Success(T value, int retryCount = 0) => new(true, value, null, retryCount);

    public static VerificationAgentCallResult<T> Failure(string reason, int retryCount = 0) => new(false, default, reason, retryCount);
}
