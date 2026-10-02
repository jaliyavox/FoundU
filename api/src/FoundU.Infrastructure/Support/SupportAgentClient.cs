using System.Net.Http.Json;
using System.Text.Json;
using FoundU.Application.Abstractions;
using FoundU.Application.Support.Dtos;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Verification;
using Microsoft.Extensions.Options;

namespace FoundU.Infrastructure.Support;

/// <summary>
/// Calls the Support Agent. Everything it returns is checked before anyone sees it: a known
/// phase, a bounded reply, and a ticket draft only when it is escalating, shaped like a ticket
/// the desk would accept.
/// </summary>
public sealed class SupportAgentClient(HttpClient http, IOptions<AiServiceOptions> options) : ISupportAgentClient
{
    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<SupportAgentResult?> RunAsync(SupportAgentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "/agents/run")
            {
                Content = JsonContent.Create(new { agent = "support", payload = request }, options: WireJson),
            };
            message.Headers.Add(AiServiceOptions.ServiceKeyHeaderName, AiServiceOptions.RequireServiceKey(options.Value));
            using var response = await http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var envelope = await response.Content.ReadFromJsonAsync<Envelope>(WireJson, cancellationToken);
            if (envelope?.Agent != "support" || envelope.Status != "completed"
                || !Guid.TryParse(envelope.AgentRunId, out _)) return null;

            var result = envelope.Output;
            if (result is null || string.IsNullOrWhiteSpace(result.Reply) || result.Reply.Length > 2000
                || result.Phase is not ("answered" or "clarify" or "escalate")
                || (result.Topic?.Length ?? 0) > 40) return null;

            // A draft exactly when escalating, and one the ticket form would accept as it is.
            if ((result.Phase == "escalate") != (result.Ticket is not null)) return null;
            if (result.Ticket is { } ticket && !ValidDraft(ticket)) return null;
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or NotSupportedException)
        {
            // Never expose response bodies, prompts, service keys or transport diagnostics.
            return null;
        }
    }

    public static bool ValidDraft(SupportTicketDraft draft) =>
        !string.IsNullOrWhiteSpace(draft.Subject) && draft.Subject.Length <= 200
        && !string.IsNullOrWhiteSpace(draft.Body) && draft.Body.Length is >= 10 and <= 4000
        && Enum.TryParse<SupportTicketCategory>(draft.Category, ignoreCase: false, out var category)
        && Enum.IsDefined(category);

    private sealed record Envelope(string? Agent, string? AgentRunId, string? Status, SupportAgentResult? Output);
}
