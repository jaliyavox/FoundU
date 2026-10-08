using System.Net;
using System.Text;
using FoundU.Application.Claims.Dtos;
using FoundU.Infrastructure.Verification;
using Microsoft.Extensions.Options;

namespace FoundU.Tests;

[Trait("Member", "Member4-Braveena")]
public sealed class VerificationAgentClientTests
{
    private static readonly Guid ClaimId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string ServiceKey = "verification-client-test-key-0123456789";

    [Fact]
    public async Task EvaluationRequestCarriesOriginalEvidenceAndPersistedQuestionAndAnswer()
    {
        var handler = new CapturingHandler(ScoringResponse("match", 1, "likely_match"));
        var client = new VerificationAgentClient(new HttpClient(handler) { BaseAddress = new Uri("http://ai.test/") }, ServiceOptions());
        var result = await client.EvaluateAnswersAsync(ClaimId,
            [new("verification-1", VerificationAnswerScoringTests.Cap)],
            new Dictionary<string, string> { ["staff_observation"] = VerificationAnswerScoringTests.Evidence },
            [new("verification-1", "Black")], "scoring-request");
        using var body = System.Text.Json.JsonDocument.Parse(handler.RequestBody);
        var payload = body.RootElement.GetProperty("payload");
        Assert.Equal(VerificationAnswerScoringTests.Evidence, payload.GetProperty("private_verification_details").GetProperty("staff_observation").GetString());
        Assert.Equal(VerificationAnswerScoringTests.Cap, payload.GetProperty("questions")[0].GetProperty("question").GetString());
        Assert.Equal("Black", payload.GetProperty("answers")[0].GetProperty("answer").GetString());
        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value!.Score);
        Assert.Empty(result.Value.ConflictingInformation!);
    }

    [Theory]
    [InlineData("no_match", .71, "unlikely_match", "Black")]
    [InlineData("match", .71, "likely_match", "Black")]
    [InlineData("insufficient", 1, "manual_review", "Black")]
    [InlineData("match", 1, "likely_match", "White")]
    [InlineData("match", 1, "likely_match", "Nike")]
    [InlineData("no_match", 0, "unlikely_match", "Black")]
    public async Task InconsistentOrFactuallyIncorrectAiEvaluationFallsBack(string evaluation, double score, string recommendation, string answer)
    {
        var client = CreateClient(ScoringResponse(evaluation, score, recommendation));
        var details = new Dictionary<string, string> { ["staff_observation"] = VerificationAnswerScoringTests.Evidence };
        VerificationAgentQuestion[] questions = [new("verification-1", VerificationAnswerScoringTests.Cap)];
        VerificationAgentAnswer[] answers = [new("verification-1", answer)];
        var result = await client.EvaluateAnswersAsync(ClaimId, questions, details, answers, "bad-scoring");
        Assert.False(result.IsSuccess);
        var fallback = FoundU.Infrastructure.Claims.SafeVerificationFallback.Evaluate(ClaimId, details, questions, answers);
        Assert.Equal(answer == "Black" ? 100 : 0, fallback.Score);
        if (answer == "Black") Assert.Empty(fallback.ConflictingInformation!);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnavailableOrMalformedAiUsesQuestionSpecificFallback(bool unavailable)
    {
        var client = CreateClient(unavailable ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : JsonResponse("{malformed"));
        var details = new Dictionary<string, string> { ["staff_observation"] = VerificationAnswerScoringTests.Evidence };
        VerificationAgentQuestion[] questions = [new("verification-1", VerificationAnswerScoringTests.Cap)];
        VerificationAgentAnswer[] answers = [new("verification-1", "Black")];
        Assert.False((await client.EvaluateAnswersAsync(ClaimId, questions, details, answers, "fallback")).IsSuccess);
        var fallback = FoundU.Infrastructure.Claims.SafeVerificationFallback.Evaluate(ClaimId, details, questions, answers);
        Assert.Equal(100, fallback.Score);
        Assert.Empty(fallback.ConflictingInformation!);
    }

    private static HttpResponseMessage ScoringResponse(string result, double score, string recommendation)
        => JsonResponse($$"""
            {"agent_run_id":"scoring","agent":"verification","status":"completed","output":{"operation":"evaluate_answers","claim_id":"{{ClaimId}}","evaluations":[{"question_id":"verification-1","result":"{{result}}","score":{{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}],"recommendation":"{{recommendation}}"},"trace":[]}
            """);

    [Theory]
    [InlineData("What brand is the bottle?", true)]
    [InlineData("What color is the bottle cap?", true)]
    [InlineData("Where is the noticeable scratch located?", true)]
    [InlineData("What color is the sticker?", true)]
    [InlineData("Where is the sticker located?", true)]
    [InlineData("What identifying mark is underneath the bottle cap?", false)]
    [InlineData("What is written on the white sticker?", false)]
    [InlineData("What shape is the sticker?", false)]
    [InlineData("What is inside the bottle?", false)]
    [InlineData("Where is the hidden compartment?", false)]
    [InlineData("What engraving does the bottle have?", false)]
    [InlineData("What is the serial number?", false)]
    public async Task GeneratedAiResponseMustBeGroundedInExactRequestEvidence(string question, bool supported)
    {
        var client = CreateClient(JsonResponse($$"""
            {"agent_run_id":"run-grounding","agent":"verification","status":"completed","output":{"operation":"generate_questions","claim_id":"{{ClaimId}}","questions":[{"question_id":"verification-1","question":"{{question}}"}],"recommendation":"manual_review"},"trace":[]}
            """));
        var result = await client.GenerateQuestionsAsync(ClaimId,
            new Dictionary<string, string> { ["observation"] = VerificationGroundingTests.Bottle }, "grounding");
        Assert.Equal(supported, result.IsSuccess);
        Assert.DoesNotContain(VerificationGroundingTests.Bottle, System.Text.Json.JsonSerializer.Serialize(result));
    }

    [Fact]
    public async Task GenerateQuestions_SendsConfiguredServiceKeyOnlyAsHeader()
    {
        var handler = new CapturingHandler(JsonResponse(
            "{\"agent_run_id\":\"run-1\",\"agent\":\"verification\",\"status\":\"completed\","
            + "\"output\":{\"operation\":\"generate_questions\",\"claim_id\":\"" + ClaimId
            + "\",\"questions\":[{\"question_id\":\"verification-1\",\"question\":\"What identifying detail can you provide about the item?\"}],"
            + "\"recommendation\":\"manual_review\"}}"));
        var client = new VerificationAgentClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://ai.test/") },
            ServiceOptions());

        var result = await client.GenerateQuestionsAsync(
            ClaimId, new Dictionary<string, string> { ["detail"] = "private detail" }, "correlation-1");

        Assert.True(result.IsSuccess);
        Assert.Equal(ServiceKey, handler.Request!.Headers.GetValues(AiServiceOptions.ServiceKeyHeaderName).Single());
        Assert.DoesNotContain(ServiceKey, handler.RequestBody);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short-key")]
    [InlineData("replace-with-strong-random-secret")]
    [InlineData("distinctive-control-secret-0123456789\r")]
    [InlineData("distinctive-control-secret-0123456789\n")]
    public async Task InvalidServiceKey_FailsBeforeSendingUnauthenticatedRequest(string serviceKey)
    {
        // The key is checked when a call is made, not when the client is built: a constructor
        // throw took down every service that depends on this client, which on a machine without
        // the key meant all of /api/claims. What must still hold: no request leaves without a
        // valid key, and the key never appears in what comes back.
        var handler = new NoRequestHandler();
        var client = new VerificationAgentClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://ai.test/") },
            ServiceOptions(serviceKey));
        var result = await client.GenerateQuestionsAsync(
            ClaimId, new Dictionary<string, string> { ["detail"] = "private detail" }, "correlation-1");

        Assert.False(result.IsSuccess);
        Assert.False(handler.WasCalled);
        if (serviceKey.Trim().Length > 0)
            Assert.DoesNotContain(serviceKey, result.FailureReason ?? string.Empty);
    }

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
        var evaluationScore = evaluationResult == "match" ? 1.0 : evaluationResult == "partial_match" ? .5 : 0;
        var client = CreateClient(JsonResponse($$"""
            {"agent_run_id":"run-1","agent":"verification","status":"completed","output":{"operation":"evaluate_answers","claim_id":"{{ClaimId}}","evaluations":[{"question_id":"verification-1","result":"{{evaluationResult}}","score":{{evaluationScore}}}],"recommendation":"{{recommendation}}"},"trace":[]}
            """));

        var result = await client.EvaluateAnswersAsync(
            ClaimId,
            [new("verification-1", "What distinctive mark or damage does the item have?")],
            new Dictionary<string, string> { ["distinctive_mark"] = "small crack" },
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
        var evaluationScore = evaluationResult == "insufficient" ? 0 : .5;
        var result = await EvaluateAsync(
            recommendation,
            $$"""[{"question_id":"verification-1","result":"{{evaluationResult}}","score":{{evaluationScore}}}]""");

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
        var client = new VerificationAgentClient(httpClient, ServiceOptions());

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
        var client = new VerificationAgentClient(
            new HttpClient(new TimeoutHandler()) { BaseAddress = new Uri("http://ai.test/") },
            ServiceOptions());

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
            new Dictionary<string, string> { ["detail"] = "small crack" },
            questions.Select(question => new VerificationAgentAnswer(question.QuestionId, "answer")).ToList(),
            "correlation-1");
    }

    private static VerificationAgentClient CreateClient(HttpResponseMessage response)
        => new(
            new HttpClient(new StubHandler(response)) { BaseAddress = new Uri("http://ai.test/") },
            ServiceOptions());

    private static IOptions<AiServiceOptions> ServiceOptions(string serviceKey = ServiceKey)
        => Options.Create(new AiServiceOptions { ServiceKey = serviceKey });

    private static HttpResponseMessage JsonResponse(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }

    private sealed class CapturingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return response;
        }
    }

    private sealed class NoRequestHandler : HttpMessageHandler
    {
        public bool WasCalled { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            throw new InvalidOperationException("An invalid service key must not send a request.");
        }
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
