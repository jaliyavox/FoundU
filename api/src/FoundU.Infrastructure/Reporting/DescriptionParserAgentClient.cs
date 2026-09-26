using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FoundU.Application.Abstractions;
using FoundU.Application.LostReports.Dtos;
using FoundU.Infrastructure.Verification;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace FoundU.Infrastructure.Reporting;

/// <summary>
/// Strict, best-effort client for the parser graph path. A parser/configuration failure is a
/// bounded fallback signal so normal lost-report submission can continue without AI.
/// </summary>
public sealed class DescriptionParserAgentClient : IDescriptionParserAgentClient
{
    private const int MaxItemTypeLength = 80;
    private const int MaxColorLength = 40;
    private const int MaxFeatureLength = 160;
    private const int MaxFeatures = 5;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly string? _serviceKey;
    private readonly bool _hasUsableConfiguration;
    private readonly ILogger<DescriptionParserAgentClient>? _logger;

    public DescriptionParserAgentClient(HttpClient httpClient, IOptions<AiServiceOptions> options, ILogger<DescriptionParserAgentClient>? logger = null)
    {
        _httpClient = httpClient;
        _serviceKey = options.Value.ServiceKey;
        _hasUsableConfiguration = HasUsableServiceKey(_serviceKey)
            && Uri.TryCreate(options.Value.BaseUrl, UriKind.Absolute, out _);
        _logger = logger;
    }

    public async Task<DescriptionParserAgentCallResult<DescriptionParserAgentResult>> ParseAsync(
        string description,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (!_hasUsableConfiguration)
            return DescriptionParserAgentCallResult<DescriptionParserAgentResult>.Failure("Description parser is unavailable.");

        try
        {
            _logger?.LogInformation("agent_request_started AgentName={AgentName} CorrelationId={CorrelationId}", "description_parser", correlationId);
            var send = await AiRequestRetry.SendAsync(async _ =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "agents/run")
                {
                    Content = JsonContent.Create(
                        new AgentRequest("description_parser", new ParserPayload(description), correlationId),
                        options: JsonOptions),
                };
                request.Headers.Add(AiServiceOptions.ServiceKeyHeaderName, _serviceKey!);
                return await _httpClient.SendAsync(request, cancellationToken);
            }, _logger, "description_parser", correlationId, cancellationToken);
            using var response = send.Response;
            if (!response.IsSuccessStatusCode)
                return DescriptionParserAgentCallResult<DescriptionParserAgentResult>.Failure("Description parser is unavailable.", send.RetryCount);

            var body = await response.Content.ReadFromJsonAsync<AgentResponse>(JsonOptions, cancellationToken);
            return (body is null ? InvalidResponse() : Validate(body)) with { RetryCount = send.RetryCount };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return DescriptionParserAgentCallResult<DescriptionParserAgentResult>.Failure("Description parser timed out.");
        }
        catch (HttpRequestException)
        {
            return DescriptionParserAgentCallResult<DescriptionParserAgentResult>.Failure("Description parser is unavailable.");
        }
        catch (JsonException)
        {
            return InvalidResponse();
        }
        catch (Exception)
        {
            return DescriptionParserAgentCallResult<DescriptionParserAgentResult>.Failure("Description parser failed safely.");
        }
    }

    private static DescriptionParserAgentCallResult<DescriptionParserAgentResult> Validate(AgentResponse response)
    {
        if (response.Agent != "description_parser"
            || response.Status != "completed"
            || string.IsNullOrWhiteSpace(response.AgentRunId)
            || response.Output.ValueKind != JsonValueKind.Object)
        {
            return InvalidResponse();
        }

        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "itemType", "primaryColor", "secondaryColor", "identifyingFeatures",
            "is_valid", "confidence_score", "unclear_reason",
        };
        var properties = response.Output.EnumerateObject().ToList();
        if (properties.Count != allowed.Count || properties.Any(property => !allowed.Contains(property.Name))
            || !TryGetText(response.Output, "itemType", MaxItemTypeLength, out var itemType)
            || !TryGetText(response.Output, "primaryColor", MaxColorLength, out var primaryColor)
            || !TryGetOptionalText(response.Output, "secondaryColor", MaxColorLength, out var secondaryColor)
            || !TryGetFeatures(response.Output, out var features)
            || !response.Output.TryGetProperty("is_valid", out var validElement)
            || validElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || !validElement.GetBoolean()
            || !response.Output.TryGetProperty("confidence_score", out var confidenceElement)
            || !confidenceElement.TryGetDouble(out var confidence)
            || !double.IsFinite(confidence)
            || confidence is < 0 or > 1
            || !TryGetOptionalText(response.Output, "unclear_reason", 240, out _)
            || IsPlaceholder(itemType) || IsPlaceholder(primaryColor))
        {
            return InvalidResponse();
        }

        return DescriptionParserAgentCallResult<DescriptionParserAgentResult>.Success(
            new DescriptionParserAgentResult(
                itemType, primaryColor, secondaryColor, features, (decimal)confidence, response.AgentRunId));
    }

    private static bool TryGetText(JsonElement output, string name, int maxLength, out string value)
    {
        value = string.Empty;
        return output.TryGetProperty(name, out var element)
            && element.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(element.GetString())
            && (value = element.GetString()!.Trim()).Length <= maxLength;
    }

    private static bool TryGetOptionalText(JsonElement output, string name, int maxLength, out string? value)
    {
        value = null;
        if (!output.TryGetProperty(name, out var element)) return false;
        if (element.ValueKind == JsonValueKind.Null) return true;
        if (element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString())) return false;
        value = element.GetString()!.Trim();
        return value.Length <= maxLength;
    }

    private static bool TryGetFeatures(JsonElement output, out IReadOnlyList<string> features)
    {
        features = [];
        if (!output.TryGetProperty("identifyingFeatures", out var element) || element.ValueKind != JsonValueKind.Array)
            return false;
        var values = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var feature in element.EnumerateArray())
        {
            if (feature.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(feature.GetString())) return false;
            var value = feature.GetString()!.Trim();
            if (value.Length > MaxFeatureLength || !seen.Add(value)) return false;
            values.Add(value);
        }
        if (values.Count > MaxFeatures) return false;
        features = values;
        return true;
    }

    private static bool IsPlaceholder(string value)
        => value.Equals("unknown", StringComparison.OrdinalIgnoreCase)
            || value.Equals("unspecified", StringComparison.OrdinalIgnoreCase);

    private static bool HasUsableServiceKey(string? serviceKey)
    {
        try { AiServiceOptions.RequireServiceKey(new AiServiceOptions { ServiceKey = serviceKey ?? string.Empty }); }
        catch (InvalidOperationException) { return false; }
        return true;
    }

    private static DescriptionParserAgentCallResult<DescriptionParserAgentResult> InvalidResponse()
        => DescriptionParserAgentCallResult<DescriptionParserAgentResult>.Failure("Description parser returned an invalid response.");

    private sealed record AgentRequest(string Agent, object Payload, [property: JsonPropertyName("correlation_id")] string CorrelationId);
    private sealed record ParserPayload(string Description);
    private sealed record AgentResponse(string? Agent, [property: JsonPropertyName("agent_run_id")] string? AgentRunId, string? Status, JsonElement Output);
}
