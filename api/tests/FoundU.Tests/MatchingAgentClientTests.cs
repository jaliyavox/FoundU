using System.Net;
using System.Text;
using FoundU.Application.Matching.Dtos;
using FoundU.Infrastructure.Matching;
using FoundU.Infrastructure.Verification;
using Microsoft.Extensions.Options;

namespace FoundU.Tests;

public sealed class MatchingAgentClientTests
{
    private const string ServiceKey = "matching-client-test-key-012345678901";

    [Fact]
    public async Task MatchReports_SendsOnlySafeContextAndAuthenticatedHeader()
    {
        var handler = new CapturingHandler(JsonResponse("""
            {"agent_run_id":"match-run-1","agent":"matching","status":"completed","output":{"recommendation":"match_candidate","score":1.0}}
            """));
        var client = CreateClient(handler);

        var result = await client.MatchReportsAsync(
            new("lost-1", "Backpack", "Blue"),
            new("found-1", "Laptop Bag", "Red"),
            "correlation-1");

        Assert.True(result.IsSuccess);
        Assert.Equal(ServiceKey, handler.Request!.Headers.GetValues(AiServiceOptions.ServiceKeyHeaderName).Single());
        Assert.Contains("\"agent\":\"matching\"", handler.RequestBody);
        Assert.Contains("\"operation\":\"match_reports\"", handler.RequestBody);
        Assert.Contains("\"report_id\":\"lost-1\"", handler.RequestBody);
        Assert.Contains("\"item_type\":\"Backpack\"", handler.RequestBody);
        Assert.Contains("\"primary_color\":\"Blue\"", handler.RequestBody);
        Assert.Contains("\"report_id\":\"found-1\"", handler.RequestBody);
        Assert.Contains("\"item_type\":\"Laptop Bag\"", handler.RequestBody);
        Assert.Contains("\"primary_color\":\"Red\"", handler.RequestBody);
        Assert.DoesNotContain("\"reportId\"", handler.RequestBody);
        Assert.DoesNotContain("\"itemType\"", handler.RequestBody);
        Assert.DoesNotContain("\"primaryColor\"", handler.RequestBody);
        Assert.DoesNotContain("PrivateVerificationDetails", handler.RequestBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SECRET-OWNERSHIP-DETAIL-DO-NOT-LEAK", handler.RequestBody);
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("{\"agent_run_id\":\"run\",\"agent\":\"verification\",\"status\":\"completed\",\"output\":{\"recommendation\":\"match_candidate\",\"score\":1}}")]
    [InlineData("{\"agent_run_id\":\"run\",\"agent\":\"matching\",\"status\":\"completed\",\"output\":{\"recommendation\":\"approve_claim\",\"score\":1}}")]
    [InlineData("{\"agent_run_id\":\"run\",\"agent\":\"matching\",\"status\":\"completed\",\"output\":{\"recommendation\":\"match_candidate\",\"score\":1.1}}")]
    [InlineData("{\"agent_run_id\":\"run\",\"agent\":\"matching\",\"status\":\"completed\",\"output\":{\"recommendation\":\"match_candidate\",\"score\":1,\"decision\":\"approved\"}}")]
    public async Task MatchReports_MalformedOrAuthoritativeResponse_FailsSafely(string body)
    {
        var result = await CreateClient(new StubHandler(JsonResponse(body))).MatchReportsAsync(
            new("lost-1", "Backpack", "Blue"), new("found-1", "Backpack", "Blue"), "correlation-1");

        Assert.False(result.IsSuccess);
        Assert.DoesNotContain("SECRET", result.FailureReason);
    }

    [Fact]
    public async Task MatchReports_UnavailableResponse_FailsSafely()
    {
        var result = await CreateClient(new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))).MatchReportsAsync(
            new("lost-1", "Backpack", "Blue"), new("found-1", "Backpack", "Blue"), "correlation-1");

        Assert.False(result.IsSuccess);
        Assert.Equal("Matching agent is unavailable.", result.FailureReason);
    }

    [Fact]
    public async Task MatchReports_TransientServiceUnavailable_RetriesWithSameCorrelationAndServiceKey()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            JsonResponse("{\"agent_run_id\":\"run\",\"agent\":\"matching\",\"status\":\"completed\",\"output\":{\"recommendation\":\"match_candidate\",\"score\":1}}"));

        var result = await CreateClient(handler).MatchReportsAsync(
            new("lost-1", "Backpack", "Blue"), new("found-1", "Backpack", "Blue"), "correlation-stable");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.RetryCount);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal(ServiceKey, request.ServiceKey);
            Assert.Contains("correlation-stable", request.Body);
        });
    }

    [Fact]
    public async Task MatchReports_BadRequest_IsNotRetried()
    {
        var handler = new SequenceHandler(new HttpResponseMessage(HttpStatusCode.BadRequest));
        var result = await CreateClient(handler).MatchReportsAsync(
            new("lost-1", "Backpack", "Blue"), new("found-1", "Backpack", "Blue"), "correlation-1");

        Assert.False(result.IsSuccess);
        Assert.Equal(0, result.RetryCount);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task MatchReports_RateLimited_RetriesOnlyTwice()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage((HttpStatusCode)429),
            new HttpResponseMessage((HttpStatusCode)429),
            new HttpResponseMessage((HttpStatusCode)429));
        var result = await CreateClient(handler).MatchReportsAsync(
            new("lost-1", "Backpack", "Blue"), new("found-1", "Backpack", "Blue"), "correlation-1");

        Assert.False(result.IsSuccess);
        Assert.Equal(2, result.RetryCount);
        Assert.Equal(3, handler.Requests.Count);
    }

    private static MatchingAgentClient CreateClient(HttpMessageHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("http://ai.test/") },
            Options.Create(new AiServiceOptions { ServiceKey = ServiceKey }));

    private static HttpResponseMessage JsonResponse(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }

    private sealed class SequenceHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        public List<(string Body, string? ServiceKey)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken),
                request.Headers.TryGetValues(AiServiceOptions.ServiceKeyHeaderName, out var values) ? values.SingleOrDefault() : null));
            return _responses.Dequeue();
        }
    }

    private sealed class CapturingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return response;
        }
    }
}
