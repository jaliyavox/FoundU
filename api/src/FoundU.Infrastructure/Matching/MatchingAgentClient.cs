using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FoundU.Application.Abstractions;
using FoundU.Application.Matching.Dtos;
using FoundU.Infrastructure.Verification;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace FoundU.Infrastructure.Matching;

/// <summary>
/// Strict HTTP client for FastAPI's recommendation-only matching contract. Malformed or
/// unavailable responses never become suggestions and never surface raw remote content.
/// </summary>
public sealed class MatchingAgentClient : IMatchingAgentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> AllowedRecommendations =
        ["match_candidate", "no_match", "manual_review"];
    private readonly HttpClient _httpClient;
    private readonly AiServiceOptions _options;
    private readonly ILogger<MatchingAgentClient>? _logger;

    public MatchingAgentClient(HttpClient httpClient, IOptions<AiServiceOptions> options, ILogger<MatchingAgentClient>? logger = null)
    {
        _httpClient = httpClient;
        // Not validated here. A throw in a constructor takes down every service that depends
        // on this client - on a machine without the key that meant all of /api/claims - so
        // the key is checked when a call is made, inside the try that turns any failure into
        // "the agent is unavailable", which the callers already handle by continuing by hand.
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MatchingAgentCallResult<MatchingAgentRecommendation>> MatchReportsAsync(
        MatchingAgentReportSummary lostReport,
        MatchingAgentReportSummary foundReport,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new MatchingAgentRequest(
                "matching",
                new MatchingPayload("match_reports", lostReport, foundReport),
                correlationId);
            _logger?.LogInformation("agent_request_started AgentName={AgentName} CorrelationId={CorrelationId}", "matching", correlationId);
            var send = await AiRequestRetry.SendAsync(async _ =>
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "agents/run")
                {
                    Content = JsonContent.Create(request, options: JsonOptions),
                };
                httpRequest.Headers.Add(AiServiceOptions.ServiceKeyHeaderName, AiServiceOptions.RequireServiceKey(_options));
                return await _httpClient.SendAsync(httpRequest, cancellationToken);
            }, _logger, "matching", correlationId, cancellationToken);
            using var response = send.Response;
            if (!response.IsSuccessStatusCode)
                return MatchingAgentCallResult<MatchingAgentRecommendation>.Failure("Matching agent is unavailable.", send.RetryCount);

            var body = await response.Content.ReadFromJsonAsync<AiAgentResponse>(JsonOptions, cancellationToken);
            return (body is null ? InvalidResponse() : Validate(body)) with { RetryCount = send.RetryCount };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return MatchingAgentCallResult<MatchingAgentRecommendation>.Failure("Matching agent timed out.");
        }
        catch (HttpRequestException)
        {
            return MatchingAgentCallResult<MatchingAgentRecommendation>.Failure("Matching agent is unavailable.");
        }
        catch (JsonException)
        {
            return InvalidResponse();
        }
        catch (Exception)
        {
            return MatchingAgentCallResult<MatchingAgentRecommendation>.Failure("Matching agent failed safely.");
        }
    }

    private static MatchingAgentCallResult<MatchingAgentRecommendation> Validate(AiAgentResponse response)
    {
        if (response.Agent != "matching"
            || response.Status != "completed"
            || string.IsNullOrWhiteSpace(response.AgentRunId)
            || response.Output.ValueKind != JsonValueKind.Object)
        {
            return InvalidResponse();
        }

        var properties = response.Output.EnumerateObject().ToList();
        if (properties.Count != 2
            || properties.Any(property => property.Name is not ("recommendation" or "score"))
            || !response.Output.TryGetProperty("recommendation", out var recommendationElement)
            || recommendationElement.ValueKind != JsonValueKind.String
            || !response.Output.TryGetProperty("score", out var scoreElement)
            || !scoreElement.TryGetDouble(out var score)
            || !double.IsFinite(score)
            || score is < 0 or > 1)
        {
            return InvalidResponse();
        }

        var recommendation = recommendationElement.GetString();
        if (recommendation is null || !AllowedRecommendations.Contains(recommendation))
            return InvalidResponse();

        return MatchingAgentCallResult<MatchingAgentRecommendation>.Success(
            new MatchingAgentRecommendation(recommendation, (decimal)score, response.AgentRunId));
    }

    private static MatchingAgentCallResult<MatchingAgentRecommendation> InvalidResponse()
        => MatchingAgentCallResult<MatchingAgentRecommendation>.Failure("Matching agent returned an invalid response.");

    private sealed record MatchingAgentRequest(
        string Agent,
        object Payload,
        [property: JsonPropertyName("correlation_id")] string CorrelationId);
    private sealed record MatchingPayload(
        string Operation,
        [property: JsonPropertyName("lost_report")] MatchingAgentReportSummary LostReport,
        [property: JsonPropertyName("found_report")] MatchingAgentReportSummary FoundReport);
    private sealed record AiAgentResponse(
        string? Agent,
        [property: JsonPropertyName("agent_run_id")] string? AgentRunId,
        string? Status,
        JsonElement Output);
}
