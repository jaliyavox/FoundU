using FoundU.Application.Abstractions;
using FoundU.Application.Claims.Dtos;
using FoundU.Application.Common.Exceptions;
using FoundU.Application.Common.Pagination;
using FoundU.Application.FoundReports.Dtos;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Infrastructure.Claims;

/// <summary>
/// The claims slice: a student asserting a found item is theirs, and staff deciding.
///
/// The whole design rests on one asymmetry - staff hold a detail about the item that was
/// never published, and the claimant has to produce it from memory. Nothing here ever puts
/// FoundReport.PrivateVerificationDetails into a student-reachable projection; the questions
/// are written from it, and the answers are judged against it, but the text itself stays on
/// the staff side of the wall.
/// </summary>
public class ClaimService : IClaimService
{
    private readonly FoundUDbContext _db;

    public ClaimService(FoundUDbContext db)
    {
        _db = db;
    }

    /// <summary>Statuses a claim can still move on from. The rest are the end of the road.</summary>
    private static readonly ClaimStatus[] OpenStatuses =
    [
        ClaimStatus.Pending,
        ClaimStatus.WaitingForAnswer,
        ClaimStatus.UnderReview,
        ClaimStatus.RevisionRequested,
        ClaimStatus.ManualReviewRequired,
    ];

    public async Task<ClaimDetailDto> CreateAsync(
        CreateClaimRequest request,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var lostReport = await _db.LostReports
            .FirstOrDefaultAsync(r => r.Id == request.LostReportId, cancellationToken)
            ?? throw new NotFoundAppException($"Lost report '{request.LostReportId}' was not found.");

        if (lostReport.StudentId != studentId)
        {
            throw new ForbiddenAppException("You can only claim an item against your own lost report.");
        }

        if (lostReport.Status is LostReportStatus.Withdrawn or LostReportStatus.Resolved)
        {
            throw new ConflictAppException(
                "This report is closed. Reopen the search with a new report if the item is still missing.");
        }

        var foundReport = await _db.FoundReports
            .FirstOrDefaultAsync(f => f.Id == request.FoundReportId, cancellationToken)
            ?? throw new NotFoundAppException($"Found report '{request.FoundReportId}' was not found.");

        if (foundReport.Status != FoundReportStatus.Unclaimed)
        {
            throw new ConflictAppException("This item is no longer available to claim.");
        }

        var alreadyClaiming = await _db.Claims.AnyAsync(
            c => c.StudentId == studentId
                && c.FoundReportId == request.FoundReportId
                && OpenStatuses.Contains(c.Status),
            cancellationToken);

        if (alreadyClaiming)
        {
            throw new ConflictAppException("You already have an open claim for this item.");
        }

        var claim = new Claim
        {
            StudentId = studentId,
            LostReportId = request.LostReportId,
            FoundReportId = request.FoundReportId,
            Status = ClaimStatus.Pending,
        };

        _db.Claims.Add(claim);

        // A claim in progress is what "Matched" means on the student's report - the item may
        // have been found, but nothing is settled until staff decide.
        if (lostReport.Status == LostReportStatus.Active)
        {
            MoveLostReport(lostReport, LostReportStatus.Matched, studentId, "A claim was submitted.");
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await LoadDetailAsync(claim.Id, cancellationToken);
    }

    public Task<PagedResult<ClaimListItemDto>> SearchForStudentAsync(
        Guid studentId,
        ClaimQuery query,
        CancellationToken cancellationToken = default)
        => SearchCoreAsync(query, studentId, cancellationToken);

    public Task<PagedResult<ClaimListItemDto>> SearchAsync(
        ClaimQuery query,
        CancellationToken cancellationToken = default)
        => SearchCoreAsync(query, null, cancellationToken);

    public async Task<ClaimDetailDto> GetByIdAsync(
        Guid id,
        Guid requesterId,
        bool requesterIsStaff,
        CancellationToken cancellationToken = default)
    {
        var studentId = await _db.Claims
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => (Guid?)c.StudentId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundAppException($"Claim '{id}' was not found.");

        if (!requesterIsStaff && studentId != requesterId)
        {
            throw new ForbiddenAppException("You can only read your own claims.");
        }

        return await LoadDetailAsync(id, cancellationToken);
    }

    public async Task<ClaimDetailDto> AddQuestionsAsync(
        Guid claimId,
        Guid staffId,
        AddVerificationQuestionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var claim = await _db.Claims
            .FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken)
            ?? throw new NotFoundAppException($"Claim '{claimId}' was not found.");

        if (!OpenStatuses.Contains(claim.Status))
        {
            throw new ConflictAppException("This claim has been decided and takes no more questions.");
        }

        foreach (var text in request.Questions)
        {
            _db.VerificationQuestions.Add(new VerificationQuestion
            {
                ClaimId = claim.Id,
                QuestionText = text.Trim(),
            });
        }

        MoveClaim(claim, ClaimStatus.WaitingForAnswer, staffId, "Verification questions were added.");

        await _db.SaveChangesAsync(cancellationToken);

        return await LoadDetailAsync(claim.Id, cancellationToken);
    }

    public async Task<ClaimDetailDto> SubmitAnswersAsync(
        Guid claimId,
        Guid studentId,
        SubmitClaimAnswersRequest request,
        CancellationToken cancellationToken = default)
    {
        var claim = await _db.Claims
            .Include(c => c.VerificationQuestions)
            .ThenInclude(q => q.Answer)
            .FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken)
            ?? throw new NotFoundAppException($"Claim '{claimId}' was not found.");

        if (claim.StudentId != studentId)
        {
            throw new ForbiddenAppException("You can only answer your own claims.");
        }

        if (claim.Status is not (ClaimStatus.WaitingForAnswer or ClaimStatus.RevisionRequested))
        {
            throw new ConflictAppException("This claim is not waiting for answers.");
        }

        var questionsById = claim.VerificationQuestions.ToDictionary(q => q.Id);

        // An id that is not on this claim is either a mistake or someone probing another
        // claim's questions - either way it is not answerable here.
        var unknown = request.Answers.Select(a => a.QuestionId).Where(id => !questionsById.ContainsKey(id)).ToList();
        if (unknown.Count > 0)
        {
            throw new ValidationAppException(
                nameof(SubmitClaimAnswersRequest.Answers),
                "One or more answers refer to a question that is not part of this claim.");
        }

        var answeredNow = request.Answers.Select(a => a.QuestionId).ToHashSet();
        var stillOutstanding = claim.VerificationQuestions
            .Where(q => q.Answer is null && !answeredNow.Contains(q.Id))
            .ToList();

        if (stillOutstanding.Count > 0)
        {
            throw new ValidationAppException(
                nameof(SubmitClaimAnswersRequest.Answers),
                $"{stillOutstanding.Count} question(s) still need an answer.");
        }

        foreach (var input in request.Answers)
        {
            var question = questionsById[input.QuestionId];

            // On a revision the student is rewriting an answer, not adding a second one.
            if (question.Answer is { } existing)
            {
                existing.AnswerText = input.AnswerText.Trim();
                existing.SubmittedAt = DateTime.UtcNow;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.IsCorrect = null;
            }
            else
            {
                _db.ClaimAnswers.Add(new ClaimAnswer
                {
                    ClaimId = claim.Id,
                    VerificationQuestionId = question.Id,
                    AnswerText = input.AnswerText.Trim(),
                });
            }
        }

        MoveClaim(claim, ClaimStatus.UnderReview, studentId, "The claimant answered the verification questions.");

        await _db.SaveChangesAsync(cancellationToken);

        return await LoadDetailAsync(claim.Id, cancellationToken);
    }

    public async Task<ClaimDetailDto> DecideAsync(
        Guid claimId,
        Guid staffId,
        ClaimDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<ApprovalDecisionType>(request.Decision, ignoreCase: true, out var decision))
        {
            throw new ValidationAppException(nameof(ClaimDecisionRequest.Decision), "Unknown decision.");
        }

        var claim = await _db.Claims
            .FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken)
            ?? throw new NotFoundAppException($"Claim '{claimId}' was not found.");

        if (!OpenStatuses.Contains(claim.Status))
        {
            throw new ConflictAppException("This claim has already been decided.");
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        _db.ApprovalDecisions.Add(new ApprovalDecision
        {
            ClaimId = claim.Id,
            DecidedByUserId = staffId,
            Decision = decision,
            Reason = reason,
        });

        switch (decision)
        {
            case ApprovalDecisionType.Approved:
                await ApproveAsync(claim, staffId, reason, cancellationToken);
                break;

            case ApprovalDecisionType.Rejected:
                MoveClaim(claim, ClaimStatus.Rejected, staffId, reason);
                await ReopenLostReportIfNothingElsePendingAsync(claim, staffId, cancellationToken);
                break;

            case ApprovalDecisionType.RevisionRequested:
                MoveClaim(claim, ClaimStatus.RevisionRequested, staffId, reason);
                break;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await LoadDetailAsync(claim.Id, cancellationToken);
    }

    public async Task<ClaimDetailDto> CancelAsync(
        Guid claimId,
        Guid studentId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var claim = await _db.Claims
            .FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken)
            ?? throw new NotFoundAppException($"Claim '{claimId}' was not found.");

        if (claim.StudentId != studentId)
        {
            throw new ForbiddenAppException("You can only cancel your own claims.");
        }

        if (!OpenStatuses.Contains(claim.Status))
        {
            throw new ConflictAppException("This claim has already been decided.");
        }

        MoveClaim(claim, ClaimStatus.Cancelled, studentId, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim());
        await ReopenLostReportIfNothingElsePendingAsync(claim, studentId, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        return await LoadDetailAsync(claim.Id, cancellationToken);
    }

    /* ------------------------------------------------------------------ internals */

    /// <summary>
    /// Approval is the only path that closes anything: the item is handed over, the search is
    /// over, and every other open claim on that item is now moot.
    /// </summary>
    private async Task ApproveAsync(Claim claim, Guid staffId, string? reason, CancellationToken cancellationToken)
    {
        MoveClaim(claim, ClaimStatus.Approved, staffId, reason);

        var foundReport = await _db.FoundReports
            .FirstOrDefaultAsync(f => f.Id == claim.FoundReportId, cancellationToken);

        if (foundReport is not null)
        {
            foundReport.Status = FoundReportStatus.Returned;
            foundReport.UpdatedAt = DateTime.UtcNow;
        }

        var lostReport = await _db.LostReports
            .FirstOrDefaultAsync(r => r.Id == claim.LostReportId, cancellationToken);

        if (lostReport is not null && lostReport.Status != LostReportStatus.Resolved)
        {
            MoveLostReport(lostReport, LostReportStatus.Resolved, staffId, "The claim was approved and the item returned.");
        }

        // Somebody else's open claim on the same item cannot succeed now. Closing it here is
        // kinder than leaving it pending forever, and it says why.
        var rivals = await _db.Claims
            .Where(c => c.FoundReportId == claim.FoundReportId
                && c.Id != claim.Id
                && OpenStatuses.Contains(c.Status))
            .ToListAsync(cancellationToken);

        foreach (var rival in rivals)
        {
            MoveClaim(rival, ClaimStatus.Rejected, staffId, "Another claim for this item was approved.");
            await ReopenLostReportIfNothingElsePendingAsync(rival, staffId, cancellationToken);
        }
    }

    /// <summary>
    /// A rejected or cancelled claim should not leave the student's report stuck on "Matched"
    /// - unless another claim of theirs is still live against it.
    /// </summary>
    private async Task ReopenLostReportIfNothingElsePendingAsync(
        Claim claim,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var lostReport = await _db.LostReports
            .FirstOrDefaultAsync(r => r.Id == claim.LostReportId, cancellationToken);

        if (lostReport is null || lostReport.Status != LostReportStatus.Matched) return;

        var stillOpen = await _db.Claims.AnyAsync(
            c => c.LostReportId == claim.LostReportId
                && c.Id != claim.Id
                && OpenStatuses.Contains(c.Status),
            cancellationToken);

        if (!stillOpen)
        {
            MoveLostReport(lostReport, LostReportStatus.Active, actorId, "No claim is open on this report any more.");
        }
    }

    private void MoveClaim(Claim claim, ClaimStatus to, Guid? actorId, string? reason)
    {
        _db.ClaimStatusHistories.Add(new ClaimStatusHistory
        {
            ClaimId = claim.Id,
            FromStatus = claim.Status,
            ToStatus = to,
            ChangedByUserId = actorId,
            Reason = reason,
        });

        claim.Status = to;
        claim.UpdatedAt = DateTime.UtcNow;
    }

    private void MoveLostReport(LostReport report, LostReportStatus to, Guid? actorId, string reason)
    {
        _db.LostReportStatusHistories.Add(new LostReportStatusHistory
        {
            LostReportId = report.Id,
            FromStatus = report.Status,
            ToStatus = to,
            ChangedByUserId = actorId,
            Reason = reason,
        });

        report.Status = to;
        report.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<PagedResult<ClaimListItemDto>> SearchCoreAsync(
        ClaimQuery query,
        Guid? studentId,
        CancellationToken cancellationToken)
    {
        var claims = _db.Claims.AsNoTracking();

        if (studentId is { } owner) claims = claims.Where(c => c.StudentId == owner);

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<ClaimStatus>(query.Status, ignoreCase: true, out var status))
            {
                throw new ValidationAppException(nameof(ClaimQuery.Status), $"Unknown claim status '{query.Status}'.");
            }

            claims = claims.Where(c => c.Status == status);
        }

        // Oldest first for the staff queue: a claim that has waited longest is the one that
        // should be worked next.
        claims = studentId is null
            ? claims.OrderBy(c => c.CreatedAt)
            : claims.OrderByDescending(c => c.CreatedAt);

        var totalCount = await claims.CountAsync(cancellationToken);

        var items = await claims
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(c => new ClaimListItemDto(
                c.Id,
                c.Status.ToString(),
                c.FoundReport.Category.Name,
                c.FoundReport.ItemType.Name,
                c.Student.FullName,
                c.VerificationQuestions.Count(q => q.Answer == null),
                c.CreatedAt,
                c.UpdatedAt))
            .ToListAsync(cancellationToken);

        return PagedResult<ClaimListItemDto>.Create(items, query.Page, query.PageSize, totalCount);
    }

    private async Task<ClaimDetailDto> LoadDetailAsync(Guid id, CancellationToken cancellationToken)
        => await _db.Claims
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new ClaimDetailDto(
                c.Id,
                c.Status.ToString(),
                c.StudentId,
                c.Student.FullName,
                c.LostReportId,
                c.LostReport.Description,
                new FoundReportSummaryDto(
                    c.FoundReport.Id,
                    c.FoundReport.Category.Name,
                    c.FoundReport.ItemType.Name,
                    c.FoundReport.FoundLocation.Name,
                    c.FoundReport.GeneralDescription,
                    c.FoundReport.PrimaryColor,
                    c.FoundReport.FoundAt,
                    c.FoundReport.Status.ToString()),
                c.VerificationQuestions
                    .OrderBy(q => q.CreatedAt)
                    .Select(q => new ClaimQuestionDto(
                        q.Id,
                        q.QuestionText,
                        q.Answer == null ? null : q.Answer.AnswerText,
                        q.Answer == null ? null : (DateTime?)q.Answer.SubmittedAt))
                    .ToList(),
                c.ApprovalDecisions
                    .OrderByDescending(d => d.DecidedAt)
                    .Select(d => d.Decision.ToString())
                    .FirstOrDefault(),
                c.ApprovalDecisions
                    .OrderByDescending(d => d.DecidedAt)
                    .Select(d => d.Reason)
                    .FirstOrDefault(),
                c.ApprovalDecisions
                    .OrderByDescending(d => d.DecidedAt)
                    .Select(d => d.DecidedByUser.FullName)
                    .FirstOrDefault(),
                c.ApprovalDecisions
                    .OrderByDescending(d => d.DecidedAt)
                    .Select(d => (DateTime?)d.DecidedAt)
                    .FirstOrDefault(),
                c.CreatedAt,
                c.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundAppException($"Claim '{id}' was not found.");
}
