using System.Net;
using System.Text;
using FoundU.Application.Claims.Dtos;
using FoundU.Infrastructure.Verification;

namespace FoundU.Tests;

public sealed class VerificationAgentClientTests
{
    private static readonly Guid ClaimId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task GenerateQuestions_ValidResponse_ReturnsOnlySafeQuestionFields()
    {
        const string secret = "small crack near charging port";
        var client = CreateClient(JsonResponse($$"""
            {"agent_run_id":"run-1","agent":"verification","status":"completed","output":{"operation":"generate_questions","claim_id":"{{ClaimId}}","questions":[{"question_id":"verification-1","question":"What distinctive mark or damage does the item have?"}],"recommendation":"manual_review"},"trace":[]}
            """));

        var result = await client.GenerateQuestionsAsync(
            ClaimId, new Dictionary<string, string> { ["distinctive_mark"] = secret }, "correlation-1");

        Assert.True(result.IsSuccess);
        Assert.Equal("verification-1", result.Value!.Questions.Single().QuestionId);
        Assert.DoesNotContain(secret, System.Text.Json.JsonSerializer.Serialize(result.Value));
    }

    [Theory]
    [InlineData("approved")]
    [InlineData("rejected")]
    [InlineData("unknown_recommendation")]
    public async Task EvaluateAnswers_InvalidRecommendationOrDecision_IsRejected(string recommendation)
    {
        var decisionField = recommendation is "approved" or "rejected"
            ? $",\"decision\":\"{recommendation}\""
            : string.Empty;
        var client = CreateClient(JsonResponse($$"""
            {"agent_run_id":"run-1","agent":"verification","status":"completed","output":{"operation":"evaluate_answers","claim_id":"{{ClaimId}}","recommendation":"{{recommendation}}"{{decisionField}}},"trace":[]}
            """));

        var result = await client.EvaluateAnswersAsync(
            ClaimId,
            [new("verification-1", "What distinctive mark or damage does the item have?")],
            new Dictionary<string, string> { ["distinctive_mark"] = "secret" },
            [new("verification-1", "answer")],
            "correlation-1");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.DoesNotContain("secret", result.FailureReason);
    }

    [Theory]
    [InlineData("manual_review")]
    [InlineData("unlikely_match")]
    [InlineData("likely_match")]
    public async Task EvaluateAnswers_AllowedRecommendation_IsReturnedOnlyAsRecommendation(string recommendation)
    {
        var evaluationResult = recommendation switch
        {
            "likely_match" => "match",
            "unlikely_match" => "no_match",
            _ => "partial_match",
        };
        var client = CreateClient(JsonResponse($$"""
            {"agent_run_id":"run-1","agent":"verification","status":"completed","output":{"operation":"evaluate_answers","claim_id":"{{ClaimId}}","evaluations":[{"question_id":"verification-1","result":"{{evaluationResult}}","score":1.0}],"recommendation":"{{recommendation}}"},"trace":[]}
            """));

        var result = await client.EvaluateAnswersAsync(
            ClaimId,
            [new("verification-1", "What distinctive mark or damage does the item have?")],
            new Dictionary<string, string> { ["distinctive_mark"] = "secret" },
            [new("verification-1", "answer")],
            "correlation-1");

        Assert.True(result.IsSuccess);
        Assert.Equal(recommendation, result.Value!.Recommendation);
    }

    [Theory]
    [InlineData("manual_review", "partial_match")]
    [InlineData("manual_review", "insufficient")]
    [InlineData("likely_match", "partial_match")]
    [InlineData("unlikely_match", "match")]
    [InlineData("manual_review", "match")]
    [InlineData("manual_review", "no_match")]
    [InlineData("manual_review", "invalid")]
    public async Task EvaluateAnswers_RecommendationAndResultMustAgree(string recommendation, string evaluationResult)
    {
        var result = await EvaluateAsync(
            recommendation,
            $$"""[{"question_id":"verification-1","result":"{{evaluationResult}}","score":0.5}]""");

        var shouldSucceed = (recommendation, evaluationResult) is ("manual_review", "partial_match")
            or ("manual_review", "insufficient");
        Assert.Equal(shouldSucceed, result.IsSuccess);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[{\"question_id\":\"verification-1\",\"result\":\"match\",\"score\":1},{\"question_id\":\"verification-1\",\"result\":\"match\",\"score\":1}]")]
    [InlineData("[{\"question_id\":\"unknown\",\"result\":\"match\",\"score\":1}]")]
    [InlineData("[{\"question_id\":\"verification-1\",\"result\":\"match\",\"score\":-0.1}]")]
    [InlineData("[{\"question_id\":\"verification-1\",\"result\":\"match\",\"score\":1.1}]")]
    [InlineData("[{\"question_id\":\"verification-1\",\"result\":\"match\",\"score\":\"NaN\"}]")]
    [InlineData("[{\"question_id\":\"verification-1\",\"result\":\"match\",\"score\":\"Infinity\"}]")]
    public async Task EvaluateAnswers_InvalidEvaluations_AreRejected(string evaluations)
    {
        var result = await EvaluateAsync("likely_match", evaluations);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task EvaluateAnswers_MissingOneOfTwoQuestionEvaluations_IsRejected()
    {
        var result = await EvaluateAsync(
            "likely_match",
            """[{"question_id":"verification-1","result":"match","score":1}]""",
            [
                new("verification-1", "Question one"),
                new("verification-2", "Question two"),
            ]);

        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData("likely_match")]
    [InlineData("unlikely_match")]
    public async Task GenerateQuestions_NonManualRecommendation_IsRejected(string recommendation)
    {
        var client = CreateClient(JsonResponse(
            "{\"agent_run_id\":\"run-1\",\"agent\":\"verification\",\"status\":\"completed\","
            + "\"output\":{\"operation\":\"generate_questions\",\"claim_id\":\"" + ClaimId
            + "\",\"questions\":[{\"question_id\":\"verification-1\",\"question\":\"Question one\"}],"
            + "\"recommendation\":\"" + recommendation + "\"}}"));

        var result = await client.GenerateQuestionsAsync(
            ClaimId, new Dictionary<string, string> { ["detail"] = "secret" }, "correlation-1");

        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("{\"agent\":\"matching\",\"status\":\"completed\",\"output\":{}}")]
    public async Task MalformedOrWrongAgentResponse_FailsSafely(string body)
    {
        var client = CreateClient(JsonResponse(body));
        var result = await client.GenerateQuestionsAsync(
            ClaimId, new Dictionary<string, string> { ["detail"] = "secret" }, "correlation-1");

        Assert.False(result.IsSuccess);
        Assert.DoesNotContain("secret", result.FailureReason);
    }

    [Fact]
    public async Task WrongClaimId_IsRejected()
    {
        var client = CreateClient(JsonResponse("""
            {"agent_run_id":"run-1","agent":"verification","status":"completed","output":{"operation":"generate_questions","claim_id":"22222222-2222-2222-2222-222222222222","questions":[{"question_id":"verification-1","question":"Question one"}],"recommendation":"manual_review"}}
            """));
        var result = await client.GenerateQuestionsAsync(
            ClaimId, new Dictionary<string, string> { ["detail"] = "secret" }, "correlation-1");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task DuplicateAgentQuestionIds_AreRejected()
    {
        var client = CreateClient(JsonResponse("""
            {"agent_run_id":"run-1","agent":"verification","status":"completed","output":{"operation":"generate_questions","claim_id":"CLAIM_ID","questions":[{"question_id":"same","question":"Question one"},{"question_id":"same","question":"Question two"}],"recommendation":"manual_review"}}
            """.Replace("CLAIM_ID", ClaimId.ToString(), StringComparison.Ordinal)));
        var result = await client.GenerateQuestionsAsync(
            ClaimId, new Dictionary<string, string> { ["detail"] = "secret" }, "correlation-1");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task DuplicateAgentQuestionText_IsRejected()
    {
        var client = CreateClient(JsonResponse("""
            {"agent_run_id":"run-1","agent":"verification","status":"completed","output":{"operation":"generate_questions","claim_id":"CLAIM_ID","questions":[{"question_id":"verification-1","question":"Same question"},{"question_id":"verification-2","question":"Same question"}],"recommendation":"manual_review"}}
            """.Replace("CLAIM_ID", ClaimId.ToString(), StringComparison.Ordinal)));

        var result = await client.GenerateQuestionsAsync(
            ClaimId, new Dictionary<string, string> { ["detail"] = "secret" }, "correlation-1");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task UnavailableAgent_FailsSafely()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var result = await client.GenerateQuestionsAsync(
            ClaimId, new Dictionary<string, string> { ["detail"] = "secret" }, "correlation-1");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task TimedOutAgent_FailsSafely()
    {
        var httpClient = new HttpClient(new TimeoutHandler())
        {
            BaseAddress = new Uri("http://ai.test/"),
            Timeout = TimeSpan.FromMilliseconds(20),
        };
        var client = new VerificationAgentClient(httpClient);

        var result = await client.GenerateQuestionsAsync(
            ClaimId, new Dictionary<string, string> { ["detail"] = "secret" }, "correlation-1");

        Assert.False(result.IsSuccess);
        Assert.Equal("Verification agent timed out.", result.FailureReason);
    }

    [Fact]
    public async Task CallerCancellation_IsPropagated()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var client = new VerificationAgentClient(new HttpClient(new TimeoutHandler())
        {
            BaseAddress = new Uri("http://ai.test/"),
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GenerateQuestionsAsync(
            ClaimId,
            new Dictionary<string, string> { ["detail"] = "secret" },
            "correlation-1",
            cancellation.Token));
    }

    private static Task<VerificationAgentCallResult<EvaluateVerificationAnswersResult>> EvaluateAsync(
        string recommendation,
        string evaluations,
        IReadOnlyList<VerificationAgentQuestion>? questions = null)
    {
        questions ??= [new("verification-1", "What distinctive mark or damage does the item have?")];
        var body = "{\"agent_run_id\":\"run-1\",\"agent\":\"verification\",\"status\":\"completed\","
            + "\"output\":{\"operation\":\"evaluate_answers\",\"claim_id\":\"" + ClaimId
            + "\",\"evaluations\":" + evaluations + ",\"recommendation\":\"" + recommendation + "\"}}";
        var client = CreateClient(JsonResponse(body));
        return client.EvaluateAnswersAsync(
            ClaimId,
            questions,
            new Dictionary<string, string> { ["detail"] = "secret" },
            questions.Select(question => new VerificationAgentAnswer(question.QuestionId, "answer")).ToList(),
            "correlation-1");
    }

    private static VerificationAgentClient CreateClient(HttpResponseMessage response)
        => new(new HttpClient(new StubHandler(response)) { BaseAddress = new Uri("http://ai.test/") });

    private static HttpResponseMessage JsonResponse(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The timeout handler should be cancelled first.");
        }
    }
}
