using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FoundU.Application.Abstractions;
using FoundU.Application.Claims.Dtos;
using Microsoft.Extensions.Options;

namespace FoundU.Infrastructure.Verification;

/// <summary>
/// Strict HTTP client for FastAPI's recommendation-only verification contract. All malformed,
/// unexpected, or unavailable responses become a generic failure for the claim service to route
/// to manual review.
/// </summary>
public sealed class VerificationAgentClient : IVerificationAgentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> AllowedRecommendations =
        ["likely_match", "manual_review", "unlikely_match"];
    private readonly HttpClient _httpClient;
    private readonly string _serviceKey;

    public VerificationAgentClient(HttpClient httpClient, IOptions<AiServiceOptions> options)
    {
        _httpClient = httpClient;
        _serviceKey = AiServiceOptions.RequireServiceKey(options.Value);
    }

    public Task<VerificationAgentCallResult<GenerateVerificationQuestionsResult>> GenerateQuestionsAsync(
        Guid claimId,
        IReadOnlyDictionary<string, string> privateVerificationDetails,
        string correlationId,
        CancellationToken cancellationToken = default)
        => SendAsync<GenerateVerificationQuestionsResult>(
            new VerificationAgentRequest(
                "verification",
                new GenerateQuestionsPayload(
                    "generate_questions", claimId.ToString(), privateVerificationDetails),
                correlationId),
            response => ValidateGenerateOutput(response, claimId),
            cancellationToken);

    public Task<VerificationAgentCallResult<EvaluateVerificationAnswersResult>> EvaluateAnswersAsync(
        Guid claimId,
        IReadOnlyList<VerificationAgentQuestion> questions,
        IReadOnlyDictionary<string, string> privateVerificationDetails,
        IReadOnlyList<VerificationAgentAnswer> answers,
        string correlationId,
        CancellationToken cancellationToken = default)
        => SendAsync<EvaluateVerificationAnswersResult>(
            new VerificationAgentRequest(
                "verification",
                new EvaluateAnswersPayload(
                    "evaluate_answers",
                    claimId.ToString(),
                    privateVerificationDetails,
                    questions,
                    answers),
                correlationId),
            response => ValidateEvaluateOutput(
                response,
                claimId,
                questions.Select(question => question.QuestionId).ToHashSet(StringComparer.Ordinal)),
            cancellationToken);

    private async Task<VerificationAgentCallResult<T>> SendAsync<T>(
        VerificationAgentRequest request,
        Func<AiAgentResponse, VerificationAgentCallResult<T>> validate,
        CancellationToken cancellationToken)
    {
        try
        {
            using var httpRequest = CreateAuthenticatedRequest(request);
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return VerificationAgentCallResult<T>.Failure("Verification agent is unavailable.");

            var body = await response.Content.ReadFromJsonAsync<AiAgentResponse>(JsonOptions, cancellationToken);
            return body is null
                ? VerificationAgentCallResult<T>.Failure("Verification agent returned an invalid response.")
                : validate(body);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return VerificationAgentCallResult<T>.Failure("Verification agent timed out.");
        }
        catch (HttpRequestException)
        {
            return VerificationAgentCallResult<T>.Failure("Verification agent is unavailable.");
        }
        catch (JsonException)
        {
            return VerificationAgentCallResult<T>.Failure("Verification agent returned an invalid response.");
        }
        catch (Exception)
        {
            return VerificationAgentCallResult<T>.Failure("Verification agent failed safely.");
        }
    }

    private HttpRequestMessage CreateAuthenticatedRequest(VerificationAgentRequest request)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "agents/run")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        httpRequest.Headers.Add(AiServiceOptions.ServiceKeyHeaderName, _serviceKey);
        return httpRequest;
    }

    private static VerificationAgentCallResult<GenerateVerificationQuestionsResult> ValidateGenerateOutput(
        AiAgentResponse response,
        Guid expectedClaimId)
    {
        if (!IsValidEnvelope(response) || HasDecisionField(response.Output))
            return VerificationAgentCallResult<GenerateVerificationQuestionsResult>.Failure("Verification agent returned an invalid response.");

        GenerateQuestionsOutput? output;
        try { output = response.Output.Deserialize<GenerateQuestionsOutput>(JsonOptions); }
        catch (JsonException) { output = null; }

        if (output is null
            || output.Operation != "generate_questions"
            || !Guid.TryParse(output.ClaimId, out var claimId)
            || claimId != expectedClaimId
            || output.Recommendation != "manual_review"
            || output.Questions is null
            || output.Questions.Count is < 1 or > 3
            || !AreValidQuestions(output.Questions))
        {
            return VerificationAgentCallResult<GenerateVerificationQuestionsResult>.Failure("Verification agent returned an invalid response.");
        }

        return VerificationAgentCallResult<GenerateVerificationQuestionsResult>.Success(
            new(expectedClaimId, output.Questions, output.Recommendation!, response.AgentRunId!));
    }

    private static VerificationAgentCallResult<EvaluateVerificationAnswersResult> ValidateEvaluateOutput(
        AiAgentResponse response,
        Guid expectedClaimId,
        IReadOnlySet<string> expectedQuestionIds)
    {
        if (!IsValidEnvelope(response) || HasDecisionField(response.Output))
            return VerificationAgentCallResult<EvaluateVerificationAnswersResult>.Failure("Verification agent returned an invalid response.");

        EvaluateAnswersOutput? output;
        try { output = response.Output.Deserialize<EvaluateAnswersOutput>(JsonOptions); }
        catch (JsonException) { output = null; }

        if (output is null
            || output.Operation != "evaluate_answers"
            || !Guid.TryParse(output.ClaimId, out var claimId)
            || claimId != expectedClaimId
            || !IsAllowedRecommendation(output.Recommendation)
            || !AreValidEvaluations(output.Evaluations, expectedQuestionIds)
            || !RecommendationMatchesEvaluations(output.Recommendation!, output.Evaluations!))
        {
            return VerificationAgentCallResult<EvaluateVerificationAnswersResult>.Failure("Verification agent returned an invalid response.");
        }

        return VerificationAgentCallResult<EvaluateVerificationAnswersResult>.Success(
            new(expectedClaimId, output.Recommendation!, response.AgentRunId!));
    }

    private static bool IsValidEnvelope(AiAgentResponse response)
        => response.Agent == "verification"
            && response.Status == "completed"
            && !string.IsNullOrWhiteSpace(response.AgentRunId)
            && response.Output.ValueKind == JsonValueKind.Object;

    private static bool HasDecisionField(JsonElement output)
        => output.EnumerateObject().Any(property => property.Name.Equals("approved", StringComparison.OrdinalIgnoreCase)
            || property.Name.Equals("rejected", StringComparison.OrdinalIgnoreCase)
            || property.Name.Equals("decision", StringComparison.OrdinalIgnoreCase)
            || property.Name.Equals("claim_approved", StringComparison.OrdinalIgnoreCase));

    private static bool IsAllowedRecommendation(string? recommendation)
        => recommendation is not null && AllowedRecommendations.Contains(recommendation);

    private static bool AreValidQuestions(IReadOnlyList<VerificationAgentQuestion> questions)
        => questions.All(q => !string.IsNullOrWhiteSpace(q.QuestionId) && !string.IsNullOrWhiteSpace(q.Question))
            && questions.Select(q => q.QuestionId).Distinct(StringComparer.Ordinal).Count() == questions.Count
            && questions.Select(q => q.Question).Distinct(StringComparer.Ordinal).Count() == questions.Count;

    private static bool AreValidEvaluations(
        IReadOnlyList<VerificationAnswerEvaluation>? evaluations,
        IReadOnlySet<string> expectedQuestionIds)
    {
        if (evaluations is null || evaluations.Count != expectedQuestionIds.Count)
            return false;

        var evaluationIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var evaluation in evaluations)
        {
            if (string.IsNullOrWhiteSpace(evaluation.QuestionId)
                || !evaluationIds.Add(evaluation.QuestionId)
                || !expectedQuestionIds.Contains(evaluation.QuestionId)
                || evaluation.Result is not ("match" or "partial_match" or "no_match" or "insufficient")
                || !double.IsFinite(evaluation.Score)
                || evaluation.Score is < 0.0 or > 1.0)
            {
                return false;
            }
        }

        return evaluationIds.SetEquals(expectedQuestionIds);
    }

    private static bool RecommendationMatchesEvaluations(
        string recommendation,
        IReadOnlyList<VerificationAnswerEvaluation> evaluations)
    {
        var expectedRecommendation = evaluations.All(evaluation => evaluation.Result == "match")
            ? "likely_match"
            : evaluations.All(evaluation => evaluation.Result == "no_match")
                ? "unlikely_match"
                : "manual_review";
        return recommendation == expectedRecommendation;
    }

    private sealed record VerificationAgentRequest(
        string Agent,
        object Payload,
        [property: JsonPropertyName("correlation_id")] string CorrelationId);
    private sealed record GenerateQuestionsPayload(
        string Operation,
        [property: JsonPropertyName("claim_id")] string ClaimId,
        [property: JsonPropertyName("private_verification_details")] IReadOnlyDictionary<string, string> PrivateVerificationDetails);
    private sealed record EvaluateAnswersPayload(
        string Operation,
        [property: JsonPropertyName("claim_id")] string ClaimId,
        [property: JsonPropertyName("private_verification_details")] IReadOnlyDictionary<string, string> PrivateVerificationDetails,
        IReadOnlyList<VerificationAgentQuestion> Questions,
        IReadOnlyList<VerificationAgentAnswer> Answers);
    private sealed record AiAgentResponse(string? Agent, [property: JsonPropertyName("agent_run_id")] string? AgentRunId, string? Status, JsonElement Output);
    private sealed record GenerateQuestionsOutput(
        string? Operation,
        [property: JsonPropertyName("claim_id")] string? ClaimId,
        IReadOnlyList<VerificationAgentQuestion>? Questions,
        string? Recommendation);
    private sealed record EvaluateAnswersOutput(
        string? Operation,
        [property: JsonPropertyName("claim_id")] string? ClaimId,
        IReadOnlyList<VerificationAnswerEvaluation>? Evaluations,
        string? Recommendation);
    private sealed record VerificationAnswerEvaluation(
        [property: JsonPropertyName("question_id")] string? QuestionId,
        string? Result,
        double Score);
}
