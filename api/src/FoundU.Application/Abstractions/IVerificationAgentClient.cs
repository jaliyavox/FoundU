using FoundU.Application.Claims.Dtos;

namespace FoundU.Application.Abstractions;

/// <summary>
/// Server-to-server boundary for the recommendation-only Verification Agent.
/// Implementations must never turn an agent response into a claim decision.
/// </summary>
public interface IVerificationAgentClient
{
    Task<VerificationAgentCallResult<GenerateVerificationQuestionsResult>> GenerateQuestionsAsync(
        Guid claimId,
        IReadOnlyDictionary<string, string> privateVerificationDetails,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<VerificationAgentCallResult<EvaluateVerificationAnswersResult>> EvaluateAnswersAsync(
        Guid claimId,
        IReadOnlyList<VerificationAgentQuestion> questions,
        IReadOnlyDictionary<string, string> privateVerificationDetails,
        IReadOnlyList<VerificationAgentAnswer> answers,
        string correlationId,
        CancellationToken cancellationToken = default);
}
