using System.Net;
using System.Text;
using FoundU.Application.LostReports.Dtos;
using FoundU.Infrastructure.Reporting;
using FoundU.Infrastructure.Verification;
using Microsoft.Extensions.Options;

namespace FoundU.Tests;

public sealed class DescriptionParserAgentClientTests
{
    private const string ServiceKey = "description-parser-test-key-012345678";
    private const string Description = "Blue backpack with a small keychain";

    [Fact]
    public async Task Parse_SendsOnlyDescriptionThroughAuthenticatedAgentRun()
    {
        var handler = new CapturingHandler(JsonResponse(ValidResponse()));
        var result = await CreateClient(handler).ParseAsync(Description, "correlation-1");

        Assert.True(result.IsSuccess);
        Assert.Equal("Backpack", result.Value!.ItemType);
        Assert.Equal(ServiceKey, handler.Request!.Headers.GetValues(AiServiceOptions.ServiceKeyHeaderName).Single());
        Assert.Contains("\"agent\":\"description_parser\"", handler.Body);
        Assert.Contains(Description, handler.Body);
        Assert.DoesNotContain(ServiceKey, handler.Body);
        Assert.DoesNotContain("studentNumber", handler.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", handler.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SECRET-OWNERSHIP-DETAIL-DO-NOT-LEAK", handler.Body);
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("{\"agent_run_id\":\"run\",\"agent\":\"matching\",\"status\":\"completed\",\"output\":{}}")]
    [InlineData("{\"agent_run_id\":\"run\",\"agent\":\"description_parser\",\"status\":\"completed\",\"output\":{\"itemType\":\"Backpack\",\"primaryColor\":\"Blue\",\"secondaryColor\":null,\"identifyingFeatures\":[],\"is_valid\":true,\"confidence_score\":1.1,\"unclear_reason\":null}}")]
    [InlineData("{\"agent_run_id\":\"run\",\"agent\":\"description_parser\",\"status\":\"completed\",\"output\":{\"itemType\":\"Backpack\",\"primaryColor\":\"Blue\",\"secondaryColor\":null,\"identifyingFeatures\":[],\"is_valid\":true,\"confidence_score\":1,\"unclear_reason\":null,\"action\":\"create_report\"}}")]
    public async Task Parse_MalformedOrAuthoritativeOutput_FailsSafely(string body)
    {
        var result = await CreateClient(new StubHandler(JsonResponse(body))).ParseAsync(Description, "correlation-1");
        Assert.False(result.IsSuccess);
        Assert.DoesNotContain(Description, result.FailureReason);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Parse_HttpFailure_FailsSafely(HttpStatusCode status)
    {
        var result = await CreateClient(new StubHandler(new HttpResponseMessage(status))).ParseAsync(Description, "correlation-1");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Parse_InvalidLocalConfiguration_FallsBackWithoutSendingARequest()
    {
        var handler = new CapturingHandler(JsonResponse(ValidResponse()));
        var client = new DescriptionParserAgentClient(new HttpClient(handler),
            Options.Create(new AiServiceOptions { ServiceKey = "" }));

        var result = await client.ParseAsync(Description, "correlation-1");

        Assert.False(result.IsSuccess);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task Parse_TimedOutOrUnavailableConnection_FailsSafely()
    {
        var timedOutClient = new DescriptionParserAgentClient(
            new HttpClient(new TimeoutHandler()) { BaseAddress = new Uri("http://ai.test/"), Timeout = TimeSpan.FromMilliseconds(20) },
            Options.Create(new AiServiceOptions { BaseUrl = "http://ai.test/", ServiceKey = ServiceKey }));
        var timedOut = await timedOutClient.ParseAsync(Description, "correlation-1");
        var unavailable = await CreateClient(new ThrowingHandler()).ParseAsync(Description, "correlation-1");

        Assert.False(timedOut.IsSuccess);
        Assert.Equal("Description parser timed out.", timedOut.FailureReason);
        Assert.False(unavailable.IsSuccess);
        Assert.Equal("Description parser is unavailable.", unavailable.FailureReason);
    }

    private static DescriptionParserAgentClient CreateClient(HttpMessageHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("http://ai.test/") },
            Options.Create(new AiServiceOptions { BaseUrl = "http://ai.test/", ServiceKey = ServiceKey }));

    private static string ValidResponse() => """
        {"agent_run_id":"parser-run-1","agent":"description_parser","status":"completed","output":{"itemType":"Backpack","primaryColor":"Blue","secondaryColor":null,"identifyingFeatures":["Small keychain"],"is_valid":true,"confidence_score":0.9,"unclear_reason":null}}
        """;
    private static HttpResponseMessage JsonResponse(string body) => new(HttpStatusCode.OK)
    { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(response);
    }

    private sealed class CapturingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string Body { get; private set; } = string.Empty;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return response;
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("connection details must not escape");
    }

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("unreachable");
        }
    }
}
