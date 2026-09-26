using System.Net.Http.Json;
using System.Text.Json;
using FoundU.Application.Intake;
using FoundU.Infrastructure.Verification;
using Microsoft.Extensions.Options;

namespace FoundU.Infrastructure.Intake;

public sealed class IntakeAgentClient(HttpClient http, IOptions<AiServiceOptions> options) : IIntakeAgentClient
{
    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<IntakeAgentResult?> RunAsync(IntakeAgentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "/agents/run")
            {
                Content = JsonContent.Create(new { agent = "intake", payload = request }, options: WireJson),
            };
            message.Headers.Add(AiServiceOptions.ServiceKeyHeaderName, AiServiceOptions.RequireServiceKey(options.Value));
            using var response = await http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            var envelope = await response.Content.ReadFromJsonAsync<Envelope>(WireJson, cancellationToken);
            if (envelope?.Agent != "intake" || envelope.Status != "completed"
                || !Guid.TryParse(envelope.AgentRunId, out _)) return null;
            var result = envelope.Output;
            if (result is null || result.Slots is null || string.IsNullOrWhiteSpace(result.Reply)
                || result.Reply.Length > 2000 || !ValidSlots(result.Slots)
                || result.Phase is not ("collecting" or "ready_to_search" or "matched" or "no_match")) return null;

            if (request.Candidates is null)
                return result.Phase is "collecting" or "ready_to_search" && result.MatchCandidateId is null ? result : null;

            if (result.Phase == "no_match") return result.MatchCandidateId is null ? result : null;
            if (result.Phase != "matched" || !Guid.TryParse(result.MatchCandidateId, out var candidateId)
                || !request.Candidates.Any(c => c.Id == candidateId)
                || result.MatchConfidence is not { } score || !double.IsFinite(score) || score is < .70 or > 1) return null;
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or NotSupportedException)
        {
            // Never expose response bodies, prompts, service keys or transport diagnostics.
            return null;
        }
    }

    private static bool ValidSlots(IntakeSlots slots) =>
        (slots.ItemType?.Length ?? 0) <= 80 && (slots.Colour?.Length ?? 0) <= 40
        && (slots.Location?.Length ?? 0) <= 80 && (slots.When?.Length ?? 0) <= 80
        && (slots.Distinctive?.Length ?? 0) <= 200;

    private sealed record Envelope(string? Agent, string? AgentRunId, string? Status, IntakeAgentResult? Output);
}
