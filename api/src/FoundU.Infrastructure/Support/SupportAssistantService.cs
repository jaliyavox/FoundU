using FoundU.Application.Abstractions;
using FoundU.Application.Support.Dtos;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Infrastructure.Support;

/// <summary>
/// The first answer to "something is wrong". Answers come from the agent's fixed help guide;
/// this service adds what only the database knows - the person's own records, as item names
/// and statuses - so an answer can be about their claim rather than claims in general.
///
/// Read-only. It never opens a ticket: when it cannot help it returns a draft, and the person
/// sends it through the ordinary ticket endpoint.
/// </summary>
public sealed class SupportAssistantService(FoundUDbContext db, ISupportAgentClient agent) : ISupportAssistantService
{
    private const int MaxHistory = 18;

    public async Task<SupportAssistantResponse> AskAsync(
        SupportAssistantRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var message = request.Message.Trim();
        var history = (request.History ?? [])
            .TakeLast(MaxHistory)
            .Select(turn => new SupportAssistantTurn(turn.Role, turn.Text.Trim()))
            .Append(new SupportAssistantTurn("user", message))
            .ToList();

        var context = await BuildContextAsync(userId, cancellationToken);
        var result = await agent.RunAsync(new SupportAgentRequest(history, context, request.LastTopic), cancellationToken);

        if (result is null)
        {
            // The service being down must never strand someone with a problem: their own words
            // become the ticket, ready to send.
            var said = history.Where(t => t.Role == "user").Select(t => t.Text).TakeLast(5).ToList();
            var subject = said[0].Length <= 80 ? said[0] : said[0][..77].TrimEnd() + "...";
            var body = "Raised through the FoundU assistant (it was unavailable).\n\nWhat I said:\n"
                + string.Join("\n", said.Select(line => $"- {line}"));
            return new SupportAssistantResponse(
                "unavailable",
                "The assistant is unavailable right now. You can still send this to the desk - check the ticket below.",
                null,
                new SupportTicketDraft(subject, nameof(SupportTicketCategory.Other), body.Length > 4000 ? body[..4000] : body));
        }

        return new SupportAssistantResponse(result.Phase, result.Reply, result.Topic, result.Ticket);
    }

    /// <summary>
    /// Names and statuses only. Collection codes, handover codes and verification answers are
    /// never read here, so they cannot reach the agent - or a model - by accident.
    /// </summary>
    private async Task<SupportAgentContext> BuildContextAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var claims = await db.Claims.AsNoTracking()
            .Where(c => c.StudentId == userId && c.Status != ClaimStatus.Cancelled)
            .OrderByDescending(c => c.UpdatedAt).Take(10)
            .Select(c => new SupportClaimSummary(c.FoundReport.ItemType.Name, c.Status.ToString()))
            .ToListAsync(cancellationToken);

        var reports = await db.LostReports.AsNoTracking()
            .Where(r => r.StudentId == userId
                && (r.Status == LostReportStatus.Active || r.Status == LostReportStatus.Matched))
            .OrderByDescending(r => r.CreatedAt).Take(10)
            .Select(r => new SupportReportSummary(r.ItemType.Name, r.Status.ToString(), r.PausedUntil != null && r.PausedUntil > now))
            .ToListAsync(cancellationToken);

        var handovers = await db.LostReportFoundClaims.AsNoTracking()
            .Where(h => (h.FinderId == userId || h.LostReport.StudentId == userId)
                && (h.Status == HandoverStatus.AwaitingHandIn || h.Status == HandoverStatus.InCustody))
            .OrderByDescending(h => h.HandoverStartedAt).Take(10)
            .Select(h => new
            {
                Item = h.LostReport.ItemType.Name,
                h.Status,
                IsFinder = h.FinderId == userId,
                h.HandoverExpiresAt,
            })
            .ToListAsync(cancellationToken);

        return new SupportAgentContext(
            claims,
            reports,
            handovers.Select(h => new SupportHandoverSummary(
                h.Item,
                h.Status.ToString(),
                h.IsFinder ? "finder" : "owner",
                h.HandoverExpiresAt is { } expires && h.Status == HandoverStatus.AwaitingHandIn
                    ? Math.Max(0, (int)Math.Ceiling((expires - now).TotalHours))
                    : null)).ToList());
    }
}
