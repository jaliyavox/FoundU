using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FoundU.Application.Abstractions;
using FoundU.Application.Claims.Dtos;
using FoundU.Infrastructure.Verification;
using Microsoft.Extensions.Options;

namespace FoundU.Infrastructure.Workflow;

/// <summary>Bounded HTTP adapter for the AI service's durable coordinator workflow endpoints.</summary>
public sealed class AgentWorkflowClient : IAgentWorkflowClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly AiServiceOptions _options;

    public AgentWorkflowClient(HttpClient httpClient, IOptions<AiServiceOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<AgentWorkflowStateDto?> StartCoordinatorAsync(Guid workflowId, string claimStatus, string verificationRecommendation, CancellationToken cancellationToken = default)
    {
        try
        {
            var body = new
            {
                agent = "coordinator",
                workflow_id = workflowId,
                payload = new
                {
                    workflow_id = workflowId.ToString(),
                    workflow_type = "claim_verification",
                    claim_status = claimStatus,
                    verification_recommendation = verificationRecommendation,
                    decision_status = "no_decision",
                    notification_state = "not_required",
                },
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, "agents/run")
            {
                Content = JsonContent.Create(body, options: JsonOptions),
            };
            request.Headers.Add(AiServiceOptions.ServiceKeyHeaderName, AiServiceOptions.RequireServiceKey(_options));
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            var wire = await response.Content.ReadFromJsonAsync<WorkflowWire>(JsonOptions, cancellationToken);
            return wire is null || wire.AgentRunId != workflowId || wire.Agent != "coordinator" || !AllowedStatus(wire.Status)
                ? null : wire.ToDto();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return null; }
    }

    public Task<AgentWorkflowStateDto?> GetAsync(Guid workflowId, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Get, workflowId, null, cancellationToken);

    public Task<AgentWorkflowStateDto?> DecideAsync(Guid workflowId, string decision, Guid decisionMakerId, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Post, workflowId, new { agent = "coordinator", decision, decision_maker_id = decisionMakerId }, cancellationToken, "approval");

    public Task<AgentWorkflowStateDto?> ResumeAsync(Guid workflowId, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Post, workflowId, new { agent = "coordinator" }, cancellationToken, "resume");

    private async Task<AgentWorkflowStateDto?> SendAsync(HttpMethod method, Guid workflowId, object? body, CancellationToken cancellationToken, string? operation = null)
    {
        try
        {
            var path = $"agents/workflows/{workflowId}" + (operation is null ? "?agent=coordinator" : $"/{operation}");
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Add(AiServiceOptions.ServiceKeyHeaderName, AiServiceOptions.RequireServiceKey(_options));
            if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("AI workflow service is unavailable.");
            var wire = await response.Content.ReadFromJsonAsync<WorkflowWire>(JsonOptions, cancellationToken);
            return wire is null || wire.AgentRunId != workflowId || wire.Agent != "coordinator" || !AllowedStatus(wire.Status)
                ? throw new InvalidOperationException("AI workflow service returned an invalid response.")
                : wire.ToDto();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException or OperationCanceledException)
        {
            throw new InvalidOperationException("AI workflow service is unavailable.");
        }
    }

    private static bool AllowedStatus(string? status) => status is "waiting_for_approval" or "approved" or "rejected" or "completed" or "failed";

    private sealed record WorkflowWire(
        [property: JsonPropertyName("agent_run_id")] Guid AgentRunId,
        string? Agent,
        string? Status,
        [property: JsonPropertyName("approval_required")] bool ApprovalRequired,
        [property: JsonPropertyName("approval_status")] string? ApprovalStatus,
        [property: JsonPropertyName("pending_action_type")] string? PendingActionType,
        [property: JsonPropertyName("safe_action_summary")] string? SafeActionSummary,
        [property: JsonPropertyName("requested_at")] DateTimeOffset? RequestedAt,
        [property: JsonPropertyName("decided_at")] DateTimeOffset? DecidedAt,
        [property: JsonPropertyName("decision_maker_id")] string? DecisionMakerId)
    {
        public AgentWorkflowStateDto ToDto() => new(AgentRunId, Status!, ApprovalRequired, ApprovalStatus ?? "not_required", PendingActionType, SafeActionSummary, RequestedAt, DecidedAt, DecisionMakerId);
    }
}
