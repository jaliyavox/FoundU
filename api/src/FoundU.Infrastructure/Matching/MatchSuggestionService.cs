using FoundU.Application.Abstractions;
using FoundU.Application.Common.Exceptions;
using FoundU.Application.Common.Pagination;
using FoundU.Application.FoundReports.Dtos;
using FoundU.Application.Matching.Dtos;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FoundU.Infrastructure.Matching;

/// <summary>
/// Suggestions link one item in storage to one lost report.
///
/// Staff can make the link by hand or request a validated Matching Agent recommendation. Nothing
/// here decides ownership - a suggestion only earns the student the right to open a claim, and
/// the claim's questions are what settle it.
/// </summary>
public class MatchSuggestionService : IMatchSuggestionService
{
    private readonly FoundUDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IMatchingAgentClient _matchingAgent;

    public MatchSuggestionService(
        FoundUDbContext db,
        INotificationService notifications,
        IMatchingAgentClient matchingAgent)
    {
        _db = db;
        _notifications = notifications;
        _matchingAgent = matchingAgent;
    }

    public async Task<MatchSuggestionDto> CreateAsync(
        CreateMatchSuggestionRequest request,
        Guid staffId,
        CancellationToken cancellationToken = default)
    {
        var lostReport = await _db.LostReports
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.LostReportId, cancellationToken)
            ?? throw new NotFoundAppException($"Lost report '{request.LostReportId}' was not found.");

        if (lostReport.Status is LostReportStatus.Withdrawn or LostReportStatus.Resolved)
        {
            throw new ConflictAppException("That report is closed - the student is no longer looking.");
        }

        var foundReport = await _db.FoundReports
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == request.FoundReportId, cancellationToken)
            ?? throw new NotFoundAppException($"Found report '{request.FoundReportId}' was not found.");

        if (foundReport.Status is not (FoundReportStatus.Unclaimed or FoundReportStatus.Posted))
        {
            throw new ConflictAppException("That item has already been handed back.");
        }

        var existing = await _db.MatchSuggestions
            .FirstOrDefaultAsync(
                m => m.LostReportId == request.LostReportId && m.FoundReportId == request.FoundReportId,
                cancellationToken);

        if (existing is not null)
        {
            throw new ConflictAppException("This item is already suggested for that report.");
        }

        var suggestion = new MatchSuggestion
        {
            LostReportId = request.LostReportId,
            FoundReportId = request.FoundReportId,
            // A person looking at two records has no confidence score, and inventing one would
            // put a number on the screen that means nothing. The DTO hides it for manual links.
            MatchScore = 0m,
            Status = MatchSuggestionStatus.Suggested,
            StaffNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
        };

        _db.MatchSuggestions.Add(suggestion);

        _db.MatchStatusHistories.Add(new MatchStatusHistory
        {
            MatchSuggestionId = suggestion.Id,
            FromStatus = MatchSuggestionStatus.Suggested,
            ToStatus = MatchSuggestionStatus.Suggested,
            ChangedByUserId = staffId,
            Reason = "Linked by staff.",
        });

        // The whole point of a suggestion is that the student hears about it. Queued onto the
        // same unit of work, so there is no state where the link exists and nobody was told.
        var itemName = await _db.ItemTypes
            .AsNoTracking()
            .Where(t => t.Id == foundReport.ItemTypeId)
            .Select(t => t.Name)
            .FirstAsync(cancellationToken);

        _notifications.Queue(
            lostReport.StudentId,
            NotificationType.PossibleMatchFound,
            $"A {itemName.ToLowerInvariant()} has been handed in",
            "Staff think it might be the one you reported. Have a look and tell them whether it is yours.",
            nameof(MatchSuggestion),
            suggestion.Id);

        await _db.SaveChangesAsync(cancellationToken);

        return await LoadAsync(suggestion.Id, cancellationToken);
    }

    public async Task<GenerateMatchSuggestionResultDto> GenerateWithAgentAsync(
        CreateMatchSuggestionRequest request,
        Guid staffId,
        CancellationToken cancellationToken = default)
    {
        var lostReport = await _db.LostReports
            .AsNoTracking()
            .Include(report => report.ItemType)
            .FirstOrDefaultAsync(report => report.Id == request.LostReportId, cancellationToken)
            ?? throw new NotFoundAppException($"Lost report '{request.LostReportId}' was not found.");
        var foundReport = await _db.FoundReports
            .AsNoTracking()
            .Include(report => report.ItemType)
            .FirstOrDefaultAsync(report => report.Id == request.FoundReportId, cancellationToken)
            ?? throw new NotFoundAppException($"Found report '{request.FoundReportId}' was not found.");

        EnsureEligibleForSuggestion(lostReport, foundReport);
        if (await _db.MatchSuggestions.AnyAsync(
                suggestion => suggestion.LostReportId == request.LostReportId
                    && suggestion.FoundReportId == request.FoundReportId,
                cancellationToken))
        {
            throw new ConflictAppException("This item is already suggested for that report.");
        }

        // The FastAPI contract intentionally accepts no descriptions or verification fields.
        // Missing comparison fields are not guessed: staff can still use the existing manual link.
        if (string.IsNullOrWhiteSpace(lostReport.PrimaryColor)
            || string.IsNullOrWhiteSpace(foundReport.PrimaryColor)
            || string.IsNullOrWhiteSpace(lostReport.ItemType.Name)
            || string.IsNullOrWhiteSpace(foundReport.ItemType.Name))
        {
            return new GenerateMatchSuggestionResultDto("manual_review", 0m, null);
        }

        var result = await _matchingAgent.MatchReportsAsync(
            ToAgentSummary(lostReport.Id, lostReport.ItemType.Name, lostReport.PrimaryColor),
            ToAgentSummary(foundReport.Id, foundReport.ItemType.Name, foundReport.PrimaryColor),
            $"matching-{Guid.NewGuid():N}",
            cancellationToken);

        var audit = CreateAgentRun(request.FoundReportId, result);
        _db.AgentRuns.Add(audit);

        if (!result.IsSuccess || result.Value is null)
        {
            await _db.SaveChangesAsync(cancellationToken);
            return new GenerateMatchSuggestionResultDto("manual_review", 0m, null);
        }

        var recommendation = result.Value;
        if (recommendation.Recommendation != "match_candidate")
        {
            await _db.SaveChangesAsync(cancellationToken);
            return new GenerateMatchSuggestionResultDto(recommendation.Recommendation, recommendation.Score, null);
        }

        var suggestion = new MatchSuggestion
        {
            LostReportId = lostReport.Id,
            FoundReportId = foundReport.Id,
            MatchScore = recommendation.Score,
            Status = MatchSuggestionStatus.Suggested,
            // This is staff-authored local context, never part of the FastAPI comparison request.
            StaffNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            GeneratedByAgentRunId = audit.Id,
        };
        _db.MatchSuggestions.Add(suggestion);
        _db.MatchStatusHistories.Add(new MatchStatusHistory
        {
            MatchSuggestionId = suggestion.Id,
            FromStatus = MatchSuggestionStatus.Suggested,
            ToStatus = MatchSuggestionStatus.Suggested,
            ChangedByUserId = staffId,
            Reason = "Suggested by Matching Agent.",
        });

        _notifications.Queue(
            lostReport.StudentId,
            NotificationType.PossibleMatchFound,
            $"A {foundReport.ItemType.Name.ToLowerInvariant()} has been handed in",
            "A possible match was found. Have a look and tell staff whether it is yours.",
            nameof(MatchSuggestion),
            suggestion.Id);

        await _db.SaveChangesAsync(cancellationToken);
        return new GenerateMatchSuggestionResultDto(
            recommendation.Recommendation,
            recommendation.Score,
            await LoadAsync(suggestion.Id, cancellationToken));
    }

    public async Task<PagedResult<MatchSuggestionDto>> GetForStudentAsync(
        Guid studentId,
        PaginationQuery query,
        CancellationToken cancellationToken = default)
    {
        // Dismissed suggestions drop off the student's list - they already said no.
        var suggestions = _db.MatchSuggestions
            .AsNoTracking()
            .Where(m => m.LostReport.StudentId == studentId && m.Status != MatchSuggestionStatus.Dismissed)
            .OrderByDescending(m => m.CreatedAt);

        var totalCount = await suggestions.CountAsync(cancellationToken);

        var items = await suggestions
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(Projection())
            .ToListAsync(cancellationToken);

        return PagedResult<MatchSuggestionDto>.Create(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<IReadOnlyList<MatchSuggestionDto>> GetForFoundReportAsync(
        Guid foundReportId,
        CancellationToken cancellationToken = default)
        => await _db.MatchSuggestions
            .AsNoTracking()
            .Where(m => m.FoundReportId == foundReportId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(Projection())
            .ToListAsync(cancellationToken);

    public async Task<MatchSuggestionDto> DismissAsync(
        Guid id,
        Guid studentId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var suggestion = await _db.MatchSuggestions
            .Include(m => m.LostReport)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new NotFoundAppException($"Suggestion '{id}' was not found.");

        if (suggestion.LostReport.StudentId != studentId)
        {
            throw new ForbiddenAppException("You can only answer suggestions on your own reports.");
        }

        if (suggestion.Status == MatchSuggestionStatus.Confirmed)
        {
            throw new ConflictAppException("You have already opened a claim for this item.");
        }

        _db.MatchStatusHistories.Add(new MatchStatusHistory
        {
            MatchSuggestionId = suggestion.Id,
            FromStatus = suggestion.Status,
            ToStatus = MatchSuggestionStatus.Dismissed,
            ChangedByUserId = studentId,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
        });

        suggestion.Status = MatchSuggestionStatus.Dismissed;
        suggestion.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return await LoadAsync(suggestion.Id, cancellationToken);
    }

    /* ------------------------------------------------------------------ internals */

    private static void EnsureEligibleForSuggestion(LostReport lostReport, FoundReport foundReport)
    {
        if (lostReport.Status is LostReportStatus.Withdrawn or LostReportStatus.Resolved)
            throw new ConflictAppException("That report is closed - the student is no longer looking.");
        if (foundReport.Status is not (FoundReportStatus.Unclaimed or FoundReportStatus.Posted))
            throw new ConflictAppException("That item has already been handed back.");
    }

    private static MatchingAgentReportSummary ToAgentSummary(Guid reportId, string itemType, string primaryColor)
        => new(reportId.ToString(), itemType.Trim(), primaryColor.Trim());

    private static AgentRun CreateAgentRun(
        Guid foundReportId,
        MatchingAgentCallResult<MatchingAgentRecommendation> result)
    {
        var outcome = result.IsSuccess && result.Value is not null
            ? JsonSerializer.Serialize(new
            {
                agent = "matching",
                remoteAgentRunId = result.Value.AgentRunId,
                recommendation = result.Value.Recommendation,
                score = result.Value.Score,
            })
            : JsonSerializer.Serialize(new { agent = "matching", outcome = "unavailable" });

        return new AgentRun
        {
            TriggerEntityType = nameof(FoundReport),
            TriggerEntityId = foundReportId,
            Objective = "Recommend a possible item match.",
            PlanJson = JsonSerializer.Serialize(new { steps = new[] { "lookup_reports", "score_reports" } }),
            Status = result.IsSuccess ? AgentRunStatus.Completed : AgentRunStatus.Failed,
            RetryCount = result.RetryCount,
            ErrorMessage = result.IsSuccess ? null : "Matching agent was unavailable.",
            FinalOutcomeJson = outcome,
            CompletedAt = DateTime.UtcNow,
        };
    }

    private async Task<MatchSuggestionDto> LoadAsync(Guid id, CancellationToken cancellationToken)
        => await _db.MatchSuggestions
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(Projection())
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundAppException($"Suggestion '{id}' was not found.");

    /// <summary>
    /// Not static: the claim lookup is a correlated subquery over the same context, which is
    /// how the student's card knows whether they have already acted on this suggestion.
    /// </summary>
    private System.Linq.Expressions.Expression<Func<MatchSuggestion, MatchSuggestionDto>> Projection()
        => m => new MatchSuggestionDto(
            m.Id,
            m.LostReportId,
            m.LostReport.Description,
            new FoundReportSummaryDto(
                m.FoundReport.Id,
                m.FoundReport.Category.Name,
                m.FoundReport.ItemType.Name,
                m.FoundReport.FoundLocation.Name,
                m.FoundReport.GeneralDescription,
                m.FoundReport.PrimaryColor,
                m.FoundReport.FoundAt,
                m.FoundReport.Status.ToString()),
            m.Status.ToString(),
            m.StaffNote,
            m.GeneratedByAgentRunId != null,
            m.GeneratedByAgentRunId == null ? null : (decimal?)m.MatchScore,
            m.LostReport.Claims
                .Where(c => c.FoundReportId == m.FoundReportId)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefault(),
            m.CreatedAt);
}
