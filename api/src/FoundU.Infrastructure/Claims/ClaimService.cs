using System.Text.Json;
using FoundU.Application.Abstractions;
using FoundU.Application.Claims.Dtos;
using FoundU.Application.Common;
using FoundU.Application.Common.Exceptions;
using FoundU.Application.Common.Pagination;
using FoundU.Application.FoundReports.Dtos;
using FoundU.Domain.Common;
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
    private readonly INotificationService _notifications;
    private readonly IVerificationAgentClient _verificationAgent;
    private readonly IHonorService _honor;
    private readonly IAgentWorkflowClient? _workflows;
    private readonly int _reviewThreshold;

    public ClaimService(
        FoundUDbContext db,
        INotificationService notifications,
        IVerificationAgentClient verificationAgent,
        IHonorService honor,
        IAgentWorkflowClient? workflows = null,
        Microsoft.Extensions.Options.IOptions<FoundU.Infrastructure.Verification.AiServiceOptions>? aiOptions = null)
    {
        _db = db;
        _notifications = notifications;
        _verificationAgent = verificationAgent;
        _honor = honor;
        _workflows = workflows;
        _reviewThreshold = Math.Clamp(aiOptions?.Value.VerificationReviewThreshold ?? new FoundU.Infrastructure.Verification.AiServiceOptions().VerificationReviewThreshold, 0, 100);
    }

    /// <summary>Statuses a claim can still move on from. The rest are the end of the road.</summary>
    internal static readonly ClaimStatus[] OpenStatuses =
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
        if (request.MatchSuggestionId is { } matchId)
        {
            var match = await _db.MatchSuggestions.Include(m => m.LostReport)
                .FirstOrDefaultAsync(m => m.Id == matchId, cancellationToken)
                ?? throw new NotFoundAppException("Match suggestion was not found.");
            if (match.LostReport.StudentId != studentId)
                throw new ForbiddenAppException("You can only claim your own match suggestions.");
            if (match.LostReportId != request.LostReportId || match.FoundReportId != request.FoundReportId)
                throw new ConflictAppException("This claim must use the report and item from the match suggestion.");
            if (match.Status == MatchSuggestionStatus.Dismissed)
                throw new ConflictAppException("This suggestion was dismissed.");
        }

        var existingClaim = await _db.Claims.FirstOrDefaultAsync(c => c.StudentId == studentId
            && c.LostReportId == request.LostReportId && c.FoundReportId == request.FoundReportId
            && (OpenStatuses.Contains(c.Status) || c.Status == ClaimStatus.Approved), cancellationToken);
        if (existingClaim is not null) return await LoadDetailAsync(existingClaim.Id, cancellationToken);

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

        if (lostReport.ItemTypeId != foundReport.ItemTypeId || lostReport.CategoryId != foundReport.CategoryId)
            throw new ConflictAppException("Only reports for the same item type and category are eligible.");

        // A finder's post cannot be claimed: nothing is at a desk yet, and the hidden detail
        // that verification rests on does not exist until a desk writes it.
        if (foundReport.Status == FoundReportStatus.Posted)
        {
            throw new ConflictAppException(
                "This item has not reached a desk yet. Once the finder hands it in, you can claim it.");
        }

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

        var reportAlreadyApproved = await _db.Claims.AnyAsync(
            c => c.LostReportId == request.LostReportId && c.Status == ClaimStatus.Approved,
            cancellationToken);

        if (reportAlreadyApproved)
        {
            throw new ConflictAppException("An item has already been approved for this report. Collect it from the desk.");
        }

        var claim = new Claim
        {
            StudentId = studentId,
            LostReportId = request.LostReportId,
            FoundReportId = request.FoundReportId,
            CustodyLocationId = foundReport.StorageLocationId,
            Status = ClaimStatus.Pending,
        };

        _db.Claims.Add(claim);
        MoveClaim(claim, ClaimStatus.Pending, studentId, "Claim submitted.");

        // A claim in progress is what "Matched" means on the student's report - the item may
        // have been found, but nothing is settled until staff decide.
        if (lostReport.Status == LostReportStatus.Active)
        {
            MoveLostReport(lostReport, LostReportStatus.Matched, studentId, "A claim was submitted.");
        }

        // If staff pointed the student at this item, the suggestion has done its job.
        var suggestion = await _db.MatchSuggestions.FirstOrDefaultAsync(
            m => m.LostReportId == request.LostReportId
                && m.FoundReportId == request.FoundReportId
                && m.Status == MatchSuggestionStatus.Suggested,
            cancellationToken);

        if (suggestion is not null)
        {
            claim.MatchSuggestionId = suggestion.Id;
            _db.MatchStatusHistories.Add(new MatchStatusHistory
            {
                MatchSuggestionId = suggestion.Id,
                FromStatus = suggestion.Status,
                ToStatus = MatchSuggestionStatus.Confirmed,
                ChangedByUserId = studentId,
                Reason = "The student opened a claim.",
            });

            suggestion.Status = MatchSuggestionStatus.Confirmed;
            suggestion.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await LoadDetailAsync(claim.Id, cancellationToken);
    }

    public async Task<ClaimDetailDto> ClaimWithoutReportAsync(
        ClaimWithoutReportRequest request,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var item = await ClaimableItemAsync(request.FoundReportId, cancellationToken);

        var existing = await _db.Claims.FirstOrDefaultAsync(
            c => c.StudentId == studentId && c.FoundReportId == item.Id
                && (OpenStatuses.Contains(c.Status) || c.Status == ClaimStatus.Approved),
            cancellationToken);
        if (existing is not null) return await LoadDetailAsync(existing.Id, cancellationToken);

        var report = await ReportForClaimAsync(item, studentId, request.Description.Trim(), studentId, cancellationToken);
        return await CreateAsync(new CreateClaimRequest(report.Id, item.Id), studentId, cancellationToken);
    }

    public async Task<ClaimDetailDto> HandOverInPersonAsync(
        InPersonHandoverRequest request,
        Guid staffId,
        CancellationToken cancellationToken = default)
    {
        // Same rule as collecting with a code: a person at the counter proves who they are.
        if (!request.OwnerIdChecked)
            throw new ValidationAppException(nameof(InPersonHandoverRequest.OwnerIdChecked),
                "Check the owner's student ID against the account before handing anything over.");

        var item = await ClaimableItemAsync(request.FoundReportId, cancellationToken);
        var student = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.StudentId, cancellationToken)
            ?? throw new NotFoundAppException("That student account was not found.");
        if (student.Role != UserRole.Student)
            throw new ValidationAppException(nameof(InPersonHandoverRequest.StudentId), "Items are handed over to student accounts.");
        if (student.IsSuspended)
            throw new ConflictAppException("This account is suspended. Resolve that before handing anything over.");

        var notes = request.VerificationNotes.Trim();

        // Their open claim on this item if they have one, else one on their chosen report,
        // else one on a report made for this hand-over.
        var claim = await _db.Claims.FirstOrDefaultAsync(
            c => c.StudentId == student.Id && c.FoundReportId == item.Id && OpenStatuses.Contains(c.Status),
            cancellationToken);
        if (claim is null)
        {
            Guid reportId;
            if (request.LostReportId is { } chosen)
            {
                var own = await _db.LostReports.FirstOrDefaultAsync(r => r.Id == chosen, cancellationToken)
                    ?? throw new NotFoundAppException("That lost report was not found.");
                if (own.StudentId != student.Id)
                    throw new ValidationAppException(nameof(InPersonHandoverRequest.LostReportId), "That report belongs to someone else.");
                reportId = own.Id;
            }
            else
            {
                reportId = (await ReportForClaimAsync(item, student.Id,
                    $"Claimed in person at the security desk: {item.GeneralDescription}", staffId, cancellationToken)).Id;
            }

            var created = await CreateAsync(new CreateClaimRequest(reportId, item.Id), student.Id, cancellationToken);
            claim = await _db.Claims.SingleAsync(c => c.Id == created.Id, cancellationToken);
        }

        await EnsureApprovableAsync(claim, staffId, "Verified in person at the security desk.", cancellationToken);

        // The notes may well name the hidden detail, so they live on the claim's history (staff
        // only) - the decision the owner reads says only how it was verified.
        _db.ClaimStatusHistories.Add(new ClaimStatusHistory
        {
            ClaimId = claim.Id,
            FromStatus = claim.Status,
            ToStatus = claim.Status,
            ChangedByUserId = staffId,
            Reason = $"Verified in person. {notes}",
        });
        _db.ApprovalDecisions.Add(new ApprovalDecision
        {
            ClaimId = claim.Id,
            DecidedByUserId = staffId,
            Decision = ApprovalDecisionType.Approved,
            Reason = "Verified in person at the security desk.",
        });
        await ApproveAsync(claim, staffId, "Verified in person at the security desk.", cancellationToken, tellOwnerWhereToCollect: false);
        await _db.SaveChangesAsync(cancellationToken);

        // Handed over on the spot: the same collection step as a code at the desk, so the item,
        // the report, the finder's thanks and the owner's receipt all follow as usual.
        return ForStaff(await CollectAsync(claim.CollectionCode!, staffId, cancellationToken));
    }

    public async Task<IReadOnlyList<DeskStudentDto>> FindStudentsAsync(string search, CancellationToken cancellationToken = default)
    {
        var term = search.Trim();
        if (term.Length < 2) return [];
        var lowered = term.ToLowerInvariant();
        return await _db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Student && !u.IsSuspended
                && ((u.Email != null && u.Email.ToLower().Contains(lowered))
                    || (u.StudentNumber != null && u.StudentNumber.ToLower() == lowered)
                    || u.FullName.ToLower().Contains(lowered)))
            .OrderBy(u => u.FullName)
            .Take(10)
            .Select(u => new DeskStudentDto(u.Id, u.FullName, u.Email, u.StudentNumber))
            .ToListAsync(cancellationToken);
    }

    /// <summary>An item a claim can be made on: at a desk, not yet promised to anyone.</summary>
    private async Task<FoundReport> ClaimableItemAsync(Guid foundReportId, CancellationToken cancellationToken)
    {
        var item = await _db.FoundReports.FirstOrDefaultAsync(f => f.Id == foundReportId, cancellationToken)
            ?? throw new NotFoundAppException($"Found item '{foundReportId}' was not found.");
        if (item.Status == FoundReportStatus.Posted)
            throw new ConflictAppException("This item has not reached a desk yet. Once the finder hands it in, you can claim it.");
        if (item.Status != FoundReportStatus.Unclaimed)
            throw new ConflictAppException("This item is no longer available to claim.");
        return item;
    }

    /// <summary>
    /// The lost report a claim needs, for an owner who never made one: the item's own type,
    /// colour and place, with their words as the description. Saved straight away so the
    /// claim can stand on it; marked so it never reaches the public feed.
    /// </summary>
    private async Task<LostReport> ReportForClaimAsync(
        FoundReport item, Guid studentId, string description, Guid actorId, CancellationToken cancellationToken)
    {
        string code;
        do code = HandoverCodes.Generate();
        while (await _db.LostReports.AnyAsync(r => r.HandInCode == code, cancellationToken));

        var report = new LostReport
        {
            StudentId = studentId,
            HandInCode = code,
            CategoryId = item.CategoryId,
            ItemTypeId = item.ItemTypeId,
            LastSeenLocationId = item.FoundLocationId,
            Description = description.Length <= 1000 ? description : description[..1000],
            PrimaryColor = item.PrimaryColor,
            EstimatedLostFromAt = item.FoundAt.AddDays(-1),
            EstimatedLostToAt = item.FoundAt,
            Status = LostReportStatus.Active,
            CreatedForClaim = true,
        };
        _db.LostReports.Add(report);
        _db.LostReportStatusHistories.Add(new LostReportStatusHistory
        {
            LostReport = report,
            FromStatus = LostReportStatus.Active,
            ToStatus = LostReportStatus.Active,
            ChangedByUserId = actorId,
            Reason = "Made for a claim on a found item - the owner had not reported it lost.",
        });
        await _db.SaveChangesAsync(cancellationToken);
        return report;
    }

    private async Task StartCoordinatorWorkflowAsync(Claim claim, string recommendation, CancellationToken cancellationToken)
    {
        if (_workflows is null) return;
        var existing = await _db.AgentRuns
            .Where(run => run.ClaimId == claim.Id
                && run.Objective == "Coordinator claim verification workflow"
                && (run.Status == AgentRunStatus.Running || run.Status == AgentRunStatus.PausedForApproval))
            .OrderByDescending(run => run.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null) return;

        var workflowId = Guid.NewGuid();
        var run = new AgentRun
        {
            ClaimId = claim.Id,
            TriggerEntityType = nameof(Claim),
            TriggerEntityId = claim.Id,
            Objective = "Coordinator claim verification workflow",
            Status = AgentRunStatus.Running,
            // This intentionally contains only an opaque remote workflow identifier.
            FinalOutcomeJson = JsonSerializer.Serialize(new { remoteAgentRunId = workflowId }),
        };
        _db.AgentRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);
        var planningStep = new AgentStep
        {
            AgentRunId = run.Id,
            AgentName = AgentName.PlannerAgent,
            StepOrder = 1,
            Task = "Coordinator planning",
            Status = AgentStepStatus.Running,
            StartedAt = DateTime.UtcNow,
        };
        _db.AgentSteps.Add(planningStep);
        await _db.SaveChangesAsync(cancellationToken);

        var workflow = await _workflows.StartCoordinatorAsync(workflowId, claim.Status.ToString(), recommendation, cancellationToken);
        if (workflow is null)
        {
            run.Status = AgentRunStatus.Failed;
            run.ErrorMessage = "Coordinator workflow is unavailable; staff review continues.";
            run.CompletedAt = DateTime.UtcNow;
            CompleteCoordinatorStep(planningStep, AgentStepStatus.Failed, "Coordinator workflow start failed.");
        }
        else
        {
            run.Status = ToAgentRunStatus(workflow.Status);
            run.RetryCount = workflow.RetryCount;
            CompleteCoordinatorStep(planningStep, AgentStepStatus.Completed, null);
            if (run.Status == AgentRunStatus.PausedForApproval)
            {
                _db.AgentSteps.Add(new AgentStep
                {
                    AgentRunId = run.Id,
                    AgentName = AgentName.PlannerAgent,
                    StepOrder = 2,
                    Task = "Waiting for human approval",
                    Status = AgentStepStatus.Running,
                    StartedAt = DateTime.UtcNow,
                });
            }
            if (run.Status is AgentRunStatus.Completed or AgentRunStatus.Failed) run.CompletedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static AgentRunStatus ToAgentRunStatus(string workflowStatus) => workflowStatus switch
    {
        "waiting_for_approval" => AgentRunStatus.PausedForApproval,
        "completed" => AgentRunStatus.Completed,
        "failed" or "rejected" => AgentRunStatus.Failed,
        _ => AgentRunStatus.Running,
    };

    private static void CompleteCoordinatorStep(AgentStep step, AgentStepStatus status, string? error)
    {
        step.Status = status;
        step.ErrorMessage = error;
        step.CompletedAt = DateTime.UtcNow;
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

        var detail = await LoadDetailAsync(id, cancellationToken);

        // The code is the owner's to quote and the desk's to type. Staff reading it off the
        // screen would make the quoting step theatre.
        // Staff opening a claim also see the item's hidden detail, to judge the answers beside
        // it. Only here: action responses (questions, AI drafts, decisions) stay without it, so
        // nothing generated in them can be mistaken for - or carry - the evidence.
        if (!requesterIsStaff) return detail;
        var staffDetail = await WithHiddenDetailAsync(ForStaff(detail), cancellationToken);
        var latest = await _db.AgentRuns.Where(r => r.ClaimId == id && r.Objective == "Verification Agent evaluate_answers")
            .OrderByDescending(r => r.StartedAt).Select(r => r.FinalOutcomeJson).FirstOrDefaultAsync(cancellationToken);
        StaffVerificationAssessment? assessment = null;
        if (latest != null)
        {
            try { assessment = JsonSerializer.Deserialize<VerificationAuditOutcome>(latest)?.Assessment; }
            catch (JsonException) { }
        }
        var evidence = await _db.FoundVerificationEvidence.Where(e => e.FoundReportId == detail.FoundItem.Id)
            .OrderBy(e => e.CreatedAt).Select(e => new StaffVerificationEvidenceDto(e.Id, e.Detail, e.RecordedByUserId, e.CreatedAt)).ToListAsync(cancellationToken);
        var item = await _db.FoundReports.SingleAsync(f => f.Id == detail.FoundItem.Id, cancellationToken);
        var unused = await UnusedOriginalEvidenceAsync(id, item, cancellationToken);
        return staffDetail with { VerificationForStaff = assessment, AdditionalEvidenceForStaff = evidence, CanUseUnusedEvidenceForFollowUp = unused.Count > 0 };
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

        if (claim.Status is not (ClaimStatus.Pending or ClaimStatus.ManualReviewRequired)
            || await _db.VerificationQuestions.AnyAsync(q => q.ClaimId == claimId, cancellationToken))
            throw new ConflictAppException("Initial questions have already been sent. Use a distinct follow-up after review.");
        var item = await _db.FoundReports.SingleAsync(f => f.Id == claim.FoundReportId, cancellationToken);
        if (item.Status != FoundReportStatus.Unclaimed)
            throw new ConflictAppException("Verification requires security custody.");
        // Staff write their own questionnaire, as they always could. The one rule is that a
        // question never gives the hidden detail away; the template grammar is for questions
        // the agent drafts, not for a person at the desk.
        if (request.Questions.Any(q => !IsStaffQuestionSafe(q, BuildPrivateVerificationDetails(item).Values)))
            throw new ValidationAppException("Questions", "Ask a non-leading question without revealing hidden evidence.");
        foreach (var text in request.Questions)
        {
            _db.VerificationQuestions.Add(new VerificationQuestion
            {
                ClaimId = claim.Id,
                QuestionText = text.Trim(),
            });
        }

        MoveClaim(claim, ClaimStatus.WaitingForAnswer, staffId, "Verification questions were added.");

        _notifications.Queue(
            claim.StudentId,
            NotificationType.VerificationQuestionAvailable,
            request.Questions.Count == 1 ? "A question about your claim" : "Questions about your claim",
            "Answer from memory to show the item is yours. Staff are comparing your answers against something that was never published.",
            nameof(Claim),
            claim.Id);

        await _db.SaveChangesAsync(cancellationToken);

        return ForStaff(await LoadDetailAsync(claim.Id, cancellationToken));
    }

    public async Task<ClaimDetailDto> GenerateQuestionsAsync(
        Guid claimId,
        Guid staffId,
        CancellationToken cancellationToken = default)
    {
        var claim = await _db.Claims
            .Include(c => c.FoundReport)
            .Include(c => c.VerificationQuestions)
            .FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken)
            ?? throw new NotFoundAppException($"Claim '{claimId}' was not found.");

        if (claim.FoundReport.Status != FoundReportStatus.Unclaimed || claim.Status is not (ClaimStatus.Pending or ClaimStatus.ManualReviewRequired))
        {
            throw new ConflictAppException("This claim is not ready for verification questions.");
        }

        if (claim.VerificationQuestions.Count > 0)
        {
            throw new ConflictAppException("This claim already has verification questions.");
        }

        var correlationId = Guid.NewGuid().ToString("N");
        // Challenge one private observation at a time; remaining fields stay unused for a
        // distinct follow-up, rather than being consumed as increasingly specific hints.
        var privateDetails = BuildPrivateVerificationDetails(claim.FoundReport)
            .OrderBy(d => d.Key, StringComparer.Ordinal).Take(1).ToDictionary(d => d.Key, d => d.Value);
        if (privateDetails.Count == 0)
        {
            return await MoveToManualReviewAfterGenerationFailureAsync(
                claim, staffId, correlationId, "Verification evidence is unavailable.", 0, cancellationToken);
        }

        var agentResult = await _verificationAgent.GenerateQuestionsAsync(
            claim.Id, privateDetails, correlationId, cancellationToken);
        if (!agentResult.IsSuccess || agentResult.Value is null
            || agentResult.Value.Questions.Any(q => !IsSafeQuestion(q.Question, privateDetails.Values)))
            agentResult = VerificationAgentCallResult<GenerateVerificationQuestionsResult>.Success(
                SafeVerificationFallback.Generate(claim.Id, privateDetails));

        var result = agentResult.Value!;
        if (result.Questions.Any(q => !IsSafeQuestion(q.Question, privateDetails.Values)))
            throw new ConflictAppException("Verification question failed the grounding or privacy check.");
        var audit = CreateVerificationAgentRun(
            claim.Id,
            "generate_questions",
            correlationId,
            result.AgentRunId,
            result.Recommendation,
            success: true,
            result.Questions,
            retryCount: agentResult.RetryCount, evidenceKeys: privateDetails.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList(),
            sourceEvidence: privateDetails);
        _db.AgentRuns.Add(audit);

        foreach (var question in result.Questions)
        {
            _db.VerificationQuestions.Add(new VerificationQuestion
            {
                ClaimId = claim.Id,
                QuestionText = question.Question,
                GeneratedByAgentRunId = audit.Id,
            });
        }

        MoveClaim(claim, ClaimStatus.WaitingForAnswer, staffId, "Verification questions were generated.");
        _notifications.Queue(
            claim.StudentId,
            NotificationType.VerificationQuestionAvailable,
            result.Questions.Count == 1 ? "A question about your claim" : "Questions about your claim",
            "Answer from memory to show the item is yours. Staff will review the result.",
            nameof(Claim),
            claim.Id);

        await _db.SaveChangesAsync(cancellationToken);
        return await LoadDetailAsync(claim.Id, cancellationToken);
    }

    private static bool IsSafeQuestion(string question, IEnumerable<string> details)
        => SafeVerificationFallback.IsSafe(question, details);

    /// <summary>A question a staff member wrote: any wording, as long as it reveals no hidden value.</summary>
    private static bool IsStaffQuestionSafe(string question, IEnumerable<string> details)
        => question.Trim().Length is >= 10 and <= 240 && SafeVerificationFallback.IsPrivateSafe(question, details);

    private async Task<Dictionary<string, string>> UnusedOriginalEvidenceAsync(Guid claimId, FoundReport item, CancellationToken ct)
    {
        var all = BuildPrivateVerificationDetails(item);
        var outcomes = await _db.AgentRuns.Where(r => r.ClaimId == claimId && r.Objective == "Verification Agent generate_questions" && r.Status == AgentRunStatus.Completed)
            .Select(r => r.FinalOutcomeJson).ToListAsync(ct);
        if (outcomes.Count == 0) return []; // Older/manual challenges cannot prove unused evidence.
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var json in outcomes)
        {
            try
            {
                var audit = JsonSerializer.Deserialize<VerificationAuditOutcome>(json!);
                if (audit?.EvidenceKeys == null) return [];
                used.UnionWith(audit.EvidenceKeys);
            }
            catch (JsonException) { return []; }
        }
        var usedValues = all.Where(d => used.Contains(d.Key)).Select(d => d.Value).ToList();
        return all.Where(d => !used.Contains(d.Key) && !SafeVerificationFallback.ReusesEvidence(d.Value, usedValues))
            .OrderBy(d => d.Key, StringComparer.Ordinal).Take(1).ToDictionary(d => d.Key, d => d.Value);
    }

    public async Task<string> DraftFollowUpAsync(Guid claimId, Guid staffId, RequestClaimFollowUp request,
        CancellationToken cancellationToken = default)
    {
        var claim = await _db.Claims.Include(c => c.FoundReport).Include(c => c.VerificationQuestions)
            .ThenInclude(q => q.Answer).FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken)
            ?? throw new NotFoundAppException("Claim was not found.");
        if (claim.Status is not (ClaimStatus.UnderReview or ClaimStatus.ManualReviewRequired)
            || claim.FoundReport.Status != FoundReportStatus.Unclaimed
            || claim.VerificationQuestions.Count == 0 || claim.VerificationQuestions.Any(q => q.Answer == null)
            || !await _db.AgentRuns.AnyAsync(r => r.ClaimId == claimId && r.Objective == "Verification Agent evaluate_answers", cancellationToken))
            throw new ConflictAppException("Wait for all answers and evaluation before drafting a follow-up.");
        var detail = request.AdditionalHiddenDetail?.Trim();
        var unused = await UnusedOriginalEvidenceAsync(claimId, claim.FoundReport, cancellationToken);
        var useUnused = string.IsNullOrWhiteSpace(detail) && unused.Count > 0;
        if (useUnused) detail = unused.Values.Single();
        if (string.IsNullOrWhiteSpace(detail) || detail.Length > 1000)
            throw new ValidationAppException("AdditionalHiddenDetail", "Record a distinct observable detail from the physical item first.");
        var previous = BuildPrivateVerificationDetails(claim.FoundReport).Values.Concat(
            await _db.FoundVerificationEvidence.Where(e => e.FoundReportId == claim.FoundReportId).Select(e => e.Detail).ToListAsync(cancellationToken)).ToList();
        if (!useUnused && SafeVerificationFallback.ReusesEvidence(detail, previous))
            throw new ConflictAppException("This evidence has already been used.");
        var details = useUnused ? unused : new Dictionary<string, string> { ["additional_observation"] = detail };
        var correlation = Guid.NewGuid().ToString("N");
        var result = await _verificationAgent.GenerateQuestionsAsync(claimId, details, correlation, cancellationToken);
        var draft = result.IsSuccess ? result.Value?.Questions.FirstOrDefault()?.Question : null;
        if (draft is null || !IsSafeQuestion(draft, [detail]))
            draft = SafeVerificationFallback.Question(detail);
        if (!IsSafeQuestion(draft, [detail]) || !SafeVerificationFallback.IsPrivateSafe(draft, previous.Append(detail))
            || claim.VerificationQuestions.Any(q => q.QuestionText.Equals(draft, StringComparison.OrdinalIgnoreCase)))
            throw new ConflictAppException("No distinct safe question is available. Staff must write and confirm one from the new observation.");
        _db.AgentRuns.Add(CreateVerificationAgentRun(claimId, "draft_follow_up", correlation,
            result.Value?.AgentRunId, "manual_review", true, [new("verification-1", draft)]));
        await _db.SaveChangesAsync(cancellationToken);
        return draft;
    }

    public async Task<ClaimDetailDto> RequestFollowUpAsync(Guid claimId, Guid staffId,
        RequestClaimFollowUp request, CancellationToken cancellationToken = default)
    {
        var claim = await _db.Claims.Include(c => c.FoundReport).Include(c => c.VerificationQuestions)
            .ThenInclude(q => q.Answer).FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken)
            ?? throw new NotFoundAppException("Claim was not found.");
        if (claim.FoundReport.Status != FoundReportStatus.Unclaimed
            || claim.Status is not (ClaimStatus.UnderReview or ClaimStatus.ManualReviewRequired)
            || claim.VerificationQuestions.Count == 0 || claim.VerificationQuestions.Any(q => q.Answer == null)
            || !await _db.AgentRuns.AnyAsync(r => r.ClaimId == claimId && r.Objective == "Verification Agent evaluate_answers", cancellationToken))
            throw new ConflictAppException("Wait for all answers and their evaluation before requesting more detail.");
        var detail = request.AdditionalHiddenDetail?.Trim();
        var unused = await UnusedOriginalEvidenceAsync(claimId, claim.FoundReport, cancellationToken);
        var useUnused = string.IsNullOrWhiteSpace(detail) && unused.Count > 0;
        if (useUnused) detail = unused.Values.Single();
        if (string.IsNullOrWhiteSpace(detail) || detail.Length > 1000)
            throw new ValidationAppException("AdditionalHiddenDetail", "Record an additional observable hidden detail from the physical item (up to 1000 characters).");
        var previous = BuildPrivateVerificationDetails(claim.FoundReport).Values.Concat(
            await _db.FoundVerificationEvidence.Where(e => e.FoundReportId == claim.FoundReportId)
                .Select(e => e.Detail).ToListAsync(cancellationToken)).ToList();
        if (!useUnused && SafeVerificationFallback.ReusesEvidence(detail, previous))
            throw new ConflictAppException("This evidence has already been used. Record a distinct physical observation.");
        var question = request.Question?.Trim();
        if (string.IsNullOrWhiteSpace(question))
            throw new ValidationAppException("Question", "Confirm a distinct follow-up question before sending it.");
        if (!IsSafeQuestion(question, [detail]) || !SafeVerificationFallback.IsPrivateSafe(question, previous.Append(detail))
            || claim.VerificationQuestions.Any(q => q.QuestionText.Equals(question, StringComparison.OrdinalIgnoreCase)))
            throw new ValidationAppException("Question", "Ask a distinct non-leading question without revealing hidden evidence.");
        var evidence = new FoundVerificationEvidence { FoundReportId = claim.FoundReportId, RecordedByUserId = staffId, Detail = detail };
        if (!useUnused) _db.FoundVerificationEvidence.Add(evidence);
        var audit = CreateVerificationAgentRun(claim.Id, "generate_questions", Guid.NewGuid().ToString("N"), null,
            "manual_review", true, [new VerificationAgentQuestion("verification-1", question)],
            evidenceKeys: useUnused ? unused.Keys.ToList() : [$"additional_{evidence.Id:N}"],
            sourceEvidence: useUnused ? unused : new Dictionary<string, string> { [$"additional_{evidence.Id:N}"] = detail });
        _db.AgentRuns.Add(audit);
        _db.VerificationQuestions.Add(new VerificationQuestion { ClaimId = claim.Id, QuestionText = question, GeneratedByAgentRunId = audit.Id });
        MoveClaim(claim, ClaimStatus.RevisionRequested, staffId,
            useUnused ? "Unused private evidence used; distinct follow-up sent." : "Additional physical evidence recorded; distinct follow-up sent.");
        _notifications.Queue(claim.StudentId, NotificationType.RevisionRequested, "The desk needs more detail",
            "A follow-up question is ready in your claim.", nameof(Claim), claim.Id);
        await _db.SaveChangesAsync(cancellationToken);
        return ForStaff(await LoadDetailAsync(claim.Id, cancellationToken));
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
            .Include(c => c.FoundReport)
            .Include(c => c.VerificationQuestions)
            .ThenInclude(q => q.GeneratedByAgentRun)
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

        if (request.Answers.Count == 0 || request.Answers.Any(a => string.IsNullOrWhiteSpace(a.AnswerText) || a.AnswerText.Trim().Length < 3))
            throw new ValidationAppException("Answers", "Provide a meaningful answer to each outstanding question.");
        var questionsById = claim.VerificationQuestions.ToDictionary(q => q.Id);
        if (request.Answers.Any(a => questionsById.TryGetValue(a.QuestionId, out var q) && q.Answer != null))
            throw new ConflictAppException("These answers have already been submitted.");

        if (request.Answers.Select(a => a.QuestionId).Distinct().Count() != request.Answers.Count)
        {
            throw new ValidationAppException(
                nameof(SubmitClaimAnswersRequest.Answers),
                "Only one answer may be submitted for each question.");
        }

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

        var correlationId = Guid.NewGuid().ToString("N");
        var allDetails = new Dictionary<string, string>(BuildPrivateVerificationDetails(claim.FoundReport));
        foreach (var evidence in await _db.FoundVerificationEvidence.Where(e => e.FoundReportId == claim.FoundReportId).ToListAsync(cancellationToken))
            allDetails[$"additional_{evidence.Id:N}"] = evidence.Detail;
        var scores = new List<double>();
        var evaluationSources = new List<string>();
        var matched = new List<string>();
        var missing = new List<string>();
        var conflicting = new List<string>();
        var individualResults = new List<StaffQuestionEvaluation>();
        foreach (var group in claim.VerificationQuestions.GroupBy(q => q.GeneratedByAgentRunId))
        {
            if (!TryBuildCanonicalAgentQuestions(group.ToList(), out var canonical))
            {
                scores.AddRange(Enumerable.Repeat(0.0, group.Count()));
                missing.Add("A verification challenge requires manual staff assessment.");
                continue;
            }
            var auditData = JsonSerializer.Deserialize<VerificationAuditOutcome>(group.First().GeneratedByAgentRun!.FinalOutcomeJson!);
            var keys = auditData?.EvidenceKeys ?? BuildPrivateVerificationDetails(claim.FoundReport).Keys.ToList();
            var evidence = allDetails.Where(d => keys.Contains(d.Key)).ToDictionary(d => d.Key, d => d.Value);
            if (auditData?.EvidenceHashes is { } hashes && (hashes.Count != evidence.Count
                || hashes.Any(h => !evidence.TryGetValue(h.Key, out var value) || EvidenceHash(value) != h.Value)))
                evidence.Clear(); // The stored question's original observation changed; require staff review.
            var answers = canonical.Select(q => new VerificationAgentAnswer(q.AgentQuestion.QuestionId,
                questionsById[q.DatabaseQuestionId].Answer!.AnswerText)).ToList();
            var agentQuestions = canonical.Select(q => q.AgentQuestion).ToList();
            var local = SafeVerificationFallback.Evaluate(claim.Id, evidence, agentQuestions, answers);
            var evaluated = await _verificationAgent.EvaluateAnswersAsync(claim.Id,
                agentQuestions, evidence, answers, correlationId, cancellationToken);
            // Obvious atomic answers are deterministic even if a fake/stale provider reports
            // an inflated score or inconsistent messages. Open descriptive questions retain
            // advisory provider scoring when its result is usable.
            var useLocal = agentQuestions.Any(q => SafeVerificationFallback.ResolveFact(q.Question, evidence.Values)?.Kind != "description");
            var result = !useLocal && evaluated.IsSuccess && evaluated.Value is not null
                && VerificationAnswerScoring.IsUsableResult(evaluated.Value, agentQuestions)
                ? evaluated.Value : local;
            evaluationSources.Add(result.AgentRunId);
            scores.AddRange(Enumerable.Repeat(Math.Clamp(result.Score!.Value, 0, 100), canonical.Count));
            matched.AddRange(result.MatchedEvidence ?? []);
            missing.AddRange(result.MissingInformation ?? []);
            conflicting.AddRange(result.ConflictingInformation ?? []);
            if (result.Evaluations is { } results)
                individualResults.AddRange(canonical.Select(q =>
                {
                    var individual = results.Single(e => e.QuestionId == q.AgentQuestion.QuestionId);
                    return new StaffQuestionEvaluation(q.DatabaseQuestionId, individual.Result, Math.Round(individual.Score * 100, 1));
                }));
        }
        var score = scores.Count == 0 ? 0 : Math.Round(scores.Average(), 1);
        var recommendation = score >= _reviewThreshold ? "Likely valid — staff review." : "More information required — manual review.";
        var assessment = new StaffVerificationAssessment(score, matched, missing, conflicting,
            scores.Count == 0 ? "Manual questions require staff assessment." : "Comparison of each answer with its question-specific staff-held evidence; staff decides ownership.", recommendation,
            individualResults);
        var evaluationRun = CreateVerificationAgentRun(claim.Id, "evaluate_answers", correlationId, string.Join("|", evaluationSources),
            score >= _reviewThreshold ? "likely_match" : "manual_review", true, null, assessment: assessment);
        _db.AgentRuns.Add(evaluationRun);
        MoveClaim(claim, score >= _reviewThreshold ? ClaimStatus.UnderReview : ClaimStatus.ManualReviewRequired,
            studentId, "Answers submitted and evaluated for staff review.");
        await _db.SaveChangesAsync(cancellationToken);

        await StartCoordinatorWorkflowAsync(claim, score >= _reviewThreshold ? "likely_match" : "manual_review", cancellationToken);

        return await LoadDetailAsync(claim.Id, cancellationToken);
    }

    public async Task<ClaimDetailDto> DecideAsync(
        Guid claimId,
        Guid staffId,
        ClaimDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<ApprovalDecisionType>(request.Decision, ignoreCase: true, out var decision) || !Enum.IsDefined(decision))
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
        if (reason is not null)
        {
            var itemEvidence = await _db.FoundReports.SingleAsync(f => f.Id == claim.FoundReportId, cancellationToken);
            var privateValues = BuildPrivateVerificationDetails(itemEvidence).Values.Concat(
                await _db.FoundVerificationEvidence.Where(e => e.FoundReportId == claim.FoundReportId).Select(e => e.Detail).ToListAsync(cancellationToken));
            if (!SafeVerificationFallback.IsPrivateSafe(reason, privateValues))
                throw new ValidationAppException("Reason", "Use a student-safe reason without hidden evidence or answer hints.");
        }
        if (decision != ApprovalDecisionType.Approved && reason is null)
            throw new ValidationAppException("Reason", "A reason is required.");
        if (decision is ApprovalDecisionType.Approved or ApprovalDecisionType.RevisionRequested)
        {
            if (claim.Status is not (ClaimStatus.UnderReview or ClaimStatus.ManualReviewRequired)
                || !await _db.ClaimAnswers.AnyAsync(a => a.ClaimId == claimId, cancellationToken)
                || await _db.VerificationQuestions.AnyAsync(q => q.ClaimId == claimId && q.Answer == null, cancellationToken)
                || !await _db.AgentRuns.AnyAsync(r => r.ClaimId == claimId && r.Objective == "Verification Agent evaluate_answers" && r.Status == AgentRunStatus.Completed, cancellationToken))
                throw new ConflictAppException("Wait for the claimant's answer and evaluation before deciding.");
        }
        if (decision == ApprovalDecisionType.RevisionRequested)
            throw new ConflictAppException("Send a distinct follow-up question using Ask for more detail.");

        if (decision == ApprovalDecisionType.Approved)
        {
            await EnsureApprovableAsync(claim, staffId, "The claim was approved.", cancellationToken);
        }

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
                _notifications.Queue(
                    claim.StudentId,
                    NotificationType.ClaimRejected,
                    "Your claim was not approved",
                    reason ?? "The desk could not match this item to you.",
                    nameof(Claim),
                    claim.Id);
                await ReopenLostReportIfNothingElsePendingAsync(claim, staffId, cancellationToken);
                break;

            case ApprovalDecisionType.RevisionRequested:
                MoveClaim(claim, ClaimStatus.RevisionRequested, staffId, reason);
                _notifications.Queue(
                    claim.StudentId,
                    NotificationType.RevisionRequested,
                    "The desk needs more detail",
                    reason ?? "Your answers were not specific enough to settle it. Try again.",
                    nameof(Claim),
                    claim.Id);
                break;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return ForStaff(await LoadDetailAsync(claim.Id, cancellationToken));
    }

    public async Task<ClaimDetailDto> OverturnAsync(
        Guid claimId,
        Guid adminId,
        OverturnClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        var claim = await _db.Claims
            .FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken)
            ?? throw new NotFoundAppException($"Claim '{claimId}' was not found.");

        if (claim.Status != ClaimStatus.Rejected)
        {
            throw new ConflictAppException("Only a rejected claim can be overturned.");
        }

        // The rejection put the owner's report back on the feed, and the item may have gone to
        // somebody else since. The same checks as any approval.
        await EnsureApprovableAsync(claim, adminId, "A rejected claim was overturned.", cancellationToken);

        var previous = await _db.ApprovalDecisions
            .Where(d => d.ClaimId == claim.Id)
            .OrderByDescending(d => d.DecidedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var reason = request.Reason.Trim();
        var originalItem = await _db.FoundReports.SingleAsync(f => f.Id == claim.FoundReportId, cancellationToken);
        var privateValues = BuildPrivateVerificationDetails(originalItem).Values.Concat(
            await _db.FoundVerificationEvidence.Where(e => e.FoundReportId == claim.FoundReportId).Select(e => e.Detail).ToListAsync(cancellationToken));
        if (!SafeVerificationFallback.IsPrivateSafe(reason, privateValues))
            throw new ValidationAppException("Reason", "Use a student-safe reason without hidden evidence or answer hints.");

        // Both rows stay: the rejection and the override. The audit is the pair of them.
        _db.ApprovalDecisions.Add(new ApprovalDecision
        {
            ClaimId = claim.Id,
            DecidedByUserId = previous?.DecidedByUserId ?? adminId,
            Decision = ApprovalDecisionType.Approved,
            Reason = reason,
            IsOverride = true,
            OverriddenByUserId = adminId,
        });

        await ApproveAsync(claim, adminId, reason, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return ForStaff(await LoadDetailAsync(claim.Id, cancellationToken));
    }

    public async Task<IReadOnlyList<AgentRunDto>> GetAgentRunsAsync(
        Guid claimId,
        CancellationToken cancellationToken = default)
    {
        var claim = await _db.Claims
            .AsNoTracking()
            .Where(c => c.Id == claimId)
            .Select(c => new { c.Id, c.FoundReportId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundAppException($"Claim '{claimId}' was not found.");

        // Two kinds of run explain a claim: the verification runs on the claim itself, and the
        // matching run on the item that suggested it - "why was this item put in front of the
        // student" is part of the story of "why is this claim here".
        var runs = await _db.AgentRuns
            .AsNoTracking()
            .Where(r => r.ClaimId == claim.Id
                || (r.TriggerEntityType == nameof(FoundReport) && r.TriggerEntityId == claim.FoundReportId))
            .OrderByDescending(r => r.StartedAt)
            .ToListAsync(cancellationToken);

        return runs.Select(r => new AgentRunDto(
                r.Id,
                AgentOf(r),
                r.Objective,
                r.Status.ToString(),
                r.ErrorMessage,
                ParseOutcome(r.FinalOutcomeJson),
                r.TriggerEntityType,
                r.StartedAt,
                r.CompletedAt))
            .ToList();
    }

    /// <summary>The run rows do not carry an agent name; the objective and outcome do.</summary>
    private static string AgentOf(AgentRun run)
    {
        if (run.Objective.Contains("Verification", StringComparison.OrdinalIgnoreCase)) return "Verification";
        if (run.Objective.Contains("match", StringComparison.OrdinalIgnoreCase)) return "Matching";
        if (run.Objective.Contains("pars", StringComparison.OrdinalIgnoreCase)) return "DescriptionParsing";
        return "Planner";
    }

    /// <summary>
    /// Parsed once here rather than handed to the client as a string. A row whose JSON does
    /// not parse still shows - with no outcome - rather than taking the panel down.
    /// </summary>
    private static System.Text.Json.JsonElement? ParseOutcome(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    public async Task<ClaimDetailDto> CollectAsync(
        string code,
        Guid staffId,
        CancellationToken cancellationToken = default)
    {
        var normalised = code.Replace(" ", "");

        // Not found rather than forbidden or conflict: a wrong code must not confirm that a
        // right one exists, and a used code looks exactly like one that never did.
        var claim = await _db.Claims
            .FirstOrDefaultAsync(c => c.CollectionCode == normalised && c.Status == ClaimStatus.Approved, cancellationToken)
            ?? throw new NotFoundAppException("No approved claim has that code.");

        var foundReport = await _db.FoundReports
            .FirstOrDefaultAsync(f => f.Id == claim.FoundReportId, cancellationToken);

        if (foundReport is not null)
        {
            _db.FoundReportStatusHistories.Add(new FoundReportStatusHistory
            {
                FoundReportId = foundReport.Id,
                FromStatus = foundReport.Status,
                ToStatus = FoundReportStatus.Returned,
                ChangedByUserId = staffId,
                Reason = "Collected by the owner with their code.",
            });
            foundReport.Status = FoundReportStatus.Returned;
            foundReport.UpdatedAt = DateTime.UtcNow;
        }

        var lostReport = await _db.LostReports
            .FirstOrDefaultAsync(r => r.Id == claim.LostReportId, cancellationToken);

        if (lostReport is not null && lostReport.Status != LostReportStatus.Resolved)
        {
            MoveLostReport(lostReport, LostReportStatus.Resolved, staffId, "Collected from the desk.");
        }

        // The person who brought it in gets the credit now that it is actually home. A staff
        // member logging an item they found themselves is not a finder for this purpose.
        if (foundReport?.FinderId is { } finderId && finderId != claim.StudentId)
        {
            var itemName = await _db.ItemTypes
                .Where(t => t.Id == foundReport.ItemTypeId)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? "item";

            _notifications.Queue(
                finderId,
                NotificationType.ItemReturnedToOwner,
                "It got home",
                $"The {itemName.ToLowerInvariant()} you handed in went home with its owner. Thank you.",
                nameof(FoundReport),
                foundReport.Id);

            await _honor.QueueAwardAsync(
                finderId,
                HonorAwardReason.HelpedReturn,
                claim.LostReportId,
                foundReport.Id,
                $"A {itemName.ToLowerInvariant()} you found reached its owner",
                cancellationToken);
        }

        // The owner's receipt - and their alarm, if someone else walked off with it.
        var collected = await _db.FoundReports
            .Where(r => r.Id == claim.FoundReportId)
            .Select(r => new { Item = r.ItemType.Name, Shelf = r.StorageLocation == null ? null : r.StorageLocation.Name })
            .FirstOrDefaultAsync(cancellationToken);
        _notifications.Queue(
            claim.StudentId,
            NotificationType.ItemCollected,
            $"You collected your {(collected?.Item ?? "item").ToLowerInvariant()}",
            (collected?.Shelf is null ? "Collected from the desk" : $"Collected from {collected.Shelf}")
                + ". The desk checked your student ID before handing it over. Wasn't you? Contact the desk "
                + "through Help & support straight away.",
            nameof(Claim),
            claim.Id);

        // Once. The code is gone the moment the item is.
        claim.CollectionCode = null;
        claim.CollectedAt = DateTime.UtcNow;
        claim.UpdatedAt = DateTime.UtcNow;

        _db.ClaimStatusHistories.Add(new ClaimStatusHistory
        {
            ClaimId = claim.Id,
            FromStatus = claim.Status,
            ToStatus = claim.Status,
            ChangedByUserId = staffId,
            Reason = "Collected. Student ID checked against the owner's name.",
        });

        await _db.SaveChangesAsync(cancellationToken);

        return await LoadDetailAsync(claim.Id, cancellationToken);
    }

    public async Task<ClaimDetailDto> GetByCollectionCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var normalised = code.Replace(" ", "");
        var claimId = await _db.Claims
            .AsNoTracking()
            .Where(c => c.CollectionCode == normalised && c.Status == ClaimStatus.Approved)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundAppException("No approved claim has that code.");

        return ForStaff(await LoadDetailAsync(claimId, cancellationToken));
    }

    /// <summary>
    /// The collection code is the owner's to quote and the desk's to type. Every detail a
    /// staff member receives passes through here, so it is never on their screen.
    /// </summary>
    private static ClaimDetailDto ForStaff(ClaimDetailDto detail) => detail with { CollectionCode = null };

    /// <summary>The item's hidden detail, for a staff reader opening the claim. Never for the owner.</summary>
    private async Task<ClaimDetailDto> WithHiddenDetailAsync(ClaimDetailDto detail, CancellationToken cancellationToken)
    {
        var hidden = await _db.FoundReports
            .AsNoTracking()
            .Where(r => r.Id == detail.FoundItem.Id)
            .Select(r => r.PrivateVerificationDetails)
            .FirstOrDefaultAsync(cancellationToken);
        return detail with { HiddenDetailForStaff = hidden };
    }

    private async Task<string> NextCollectionCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = HandoverCodes.Generate();
            if (!await _db.Claims.AnyAsync(c => c.CollectionCode == code, cancellationToken)) return code;
        }

        throw new InvalidOperationException("Could not allocate a unique collection code.");
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

        var cancelReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (cancelReason is { Length: > 500 })
        {
            throw new ValidationAppException("Reason", "Keep the reason under 500 characters.");
        }

        MoveClaim(claim, ClaimStatus.Cancelled, studentId, cancelReason);
        await ReopenLostReportIfNothingElsePendingAsync(claim, studentId, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        return await LoadDetailAsync(claim.Id, cancellationToken);
    }

    private async Task<ClaimDetailDto> MoveToManualReviewAfterGenerationFailureAsync(
        Claim claim,
        Guid staffId,
        string correlationId,
        string failureReason,
        int retryCount,
        CancellationToken cancellationToken)
    {
        RecordVerificationAgentFailure(claim.Id, "generate_questions", correlationId, failureReason, retryCount);
        MoveClaim(claim, ClaimStatus.ManualReviewRequired, staffId, "Verification requires staff review.");
        await _db.SaveChangesAsync(cancellationToken);
        return await LoadDetailAsync(claim.Id, cancellationToken);
    }

    private static IReadOnlyDictionary<string, string> BuildPrivateVerificationDetails(FoundReport foundReport)
    {
        var details = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(foundReport.PrivateVerificationAttributesJson))
        {
            try
            {
                using var document = JsonDocument.Parse(foundReport.PrivateVerificationAttributesJson);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in document.RootElement.EnumerateObject())
                    {
                        if (property.Value.ValueKind == JsonValueKind.String
                            && !string.IsNullOrWhiteSpace(property.Value.GetString()))
                        {
                            details[property.Name] = property.Value.GetString()!.Trim();
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // The free-text staff value below remains a safe fallback. Never surface JSON.
            }
        }

        if (details.Count == 0 && !string.IsNullOrWhiteSpace(foundReport.PrivateVerificationDetails))
        {
            details["staff_verification_detail"] = foundReport.PrivateVerificationDetails.Trim();
        }

        return details;
    }

    private static bool TryBuildCanonicalAgentQuestions(
        ICollection<VerificationQuestion> databaseQuestions,
        out IReadOnlyList<CanonicalAgentQuestion> canonicalQuestions)
    {
        canonicalQuestions = [];
        var runIds = databaseQuestions
            .Select(question => question.GeneratedByAgentRunId)
            .Distinct()
            .ToList();
        if (databaseQuestions.Count == 0 || runIds.Count != 1 || runIds[0] is not { } runId)
            return false;

        var run = databaseQuestions.First().GeneratedByAgentRun;
        if (run?.Id != runId || string.IsNullOrWhiteSpace(run.FinalOutcomeJson))
            return false;

        VerificationAuditOutcome? audit;
        try { audit = JsonSerializer.Deserialize<VerificationAuditOutcome>(run.FinalOutcomeJson); }
        catch (JsonException) { return false; }

        if (audit is null
            || !audit.Success
            || audit.Operation != "generate_questions"
            || audit.Questions is null
            || audit.Questions.Count != databaseQuestions.Count
            || audit.Questions.Select(question => question.QuestionId).Distinct(StringComparer.Ordinal).Count() != audit.Questions.Count
            || audit.Questions.Select(question => question.Question).Distinct(StringComparer.Ordinal).Count() != audit.Questions.Count)
        {
            return false;
        }

        if (databaseQuestions.Any(question => string.IsNullOrWhiteSpace(question.QuestionText))
            || databaseQuestions.Select(question => question.QuestionText).Distinct(StringComparer.Ordinal).Count() != databaseQuestions.Count)
        {
            return false;
        }

        var databaseByText = databaseQuestions.ToDictionary(question => question.QuestionText, StringComparer.Ordinal);
        if (audit.Questions.Any(question => !databaseByText.ContainsKey(question.Question)))
            return false;

        canonicalQuestions = audit.Questions
            .Select(question => new CanonicalAgentQuestion(databaseByText[question.Question].Id, question))
            .ToList();
        return true;
    }

    private void RecordVerificationAgentFailure(
        Guid claimId,
        string operation,
        string correlationId,
        string failureReason,
        int retryCount = 0)
    {
        _db.AgentRuns.Add(CreateVerificationAgentRun(
            claimId,
            operation,
            correlationId,
            remoteAgentRunId: null,
            recommendation: "manual_review",
            success: false,
            questions: null,
            failureReason,
            retryCount));
    }

    private static AgentRun CreateVerificationAgentRun(
        Guid claimId,
        string operation,
        string correlationId,
        string? remoteAgentRunId,
        string recommendation,
        bool success,
        IReadOnlyList<VerificationAgentQuestion>? questions,
        string? failureReason = null,
        int retryCount = 0,
        IReadOnlyList<string>? evidenceKeys = null,
        StaffVerificationAssessment? assessment = null,
        IReadOnlyDictionary<string, string>? sourceEvidence = null)
        => new()
        {
            ClaimId = claimId,
            TriggerEntityType = nameof(Claim),
            TriggerEntityId = claimId,
            Objective = $"Verification Agent {operation}",
            Status = success ? AgentRunStatus.Completed : AgentRunStatus.Failed,
            RetryCount = retryCount,
            ErrorMessage = success ? null : "Verification agent interaction requires staff review.",
            FinalOutcomeJson = JsonSerializer.Serialize(new VerificationAuditOutcome(
                operation,
                correlationId,
                remoteAgentRunId,
                recommendation,
                success,
                questions, evidenceKeys, assessment,
                sourceEvidence?.ToDictionary(e => e.Key, e => EvidenceHash(e.Value)))),
            CompletedAt = DateTime.UtcNow,
        };

    private sealed record CanonicalAgentQuestion(Guid DatabaseQuestionId, VerificationAgentQuestion AgentQuestion);

    private static string EvidenceHash(string value)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));

    // Safe audit only: this intentionally excludes hidden evidence, submitted answers, and AI trace.
    private sealed record VerificationAuditOutcome(
        string Operation,
        string CorrelationId,
        string? RemoteAgentRunId,
        string Recommendation,
        bool Success,
        IReadOnlyList<VerificationAgentQuestion>? Questions,
        IReadOnlyList<string>? EvidenceKeys = null,
        StaffVerificationAssessment? Assessment = null,
        IReadOnlyDictionary<string, string>? EvidenceHashes = null);

    /* ------------------------------------------------------------------ internals */

    /// <summary>
    /// What must be true before anyone - staff or an admin overturning - can approve a claim:
    /// the item is still on the shelf and unreserved, and the owner has not closed their
    /// report. A report back on the feed after a rejection comes off it again.
    /// </summary>
    private async Task EnsureApprovableAsync(Claim claim, Guid actorId, string reason, CancellationToken cancellationToken)
    {
        var item = await _db.FoundReports
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == claim.FoundReportId, cancellationToken);

        if (item is null || item.Status != FoundReportStatus.Unclaimed)
        {
            throw new ConflictAppException("This item is no longer in storage, so the claim cannot be approved now.");
        }

        var lostReport = await _db.LostReports
            .FirstOrDefaultAsync(r => r.Id == claim.LostReportId, cancellationToken)
            ?? throw new NotFoundAppException($"Lost report '{claim.LostReportId}' was not found.");

        if (lostReport.Status is LostReportStatus.Withdrawn or LostReportStatus.Resolved)
        {
            throw new ConflictAppException("The owner has closed their report, so the claim cannot be approved now.");
        }

        // One lost item is one found item. A second approval against the same report would
        // put two items on hold for one person.
        var alreadyApproved = await _db.Claims.AnyAsync(
            c => c.LostReportId == claim.LostReportId && c.Id != claim.Id && c.Status == ClaimStatus.Approved,
            cancellationToken);
        if (alreadyApproved)
        {
            throw new ConflictAppException("Another item has already been approved for this report.");
        }

        if (lostReport.Status == LostReportStatus.Active)
        {
            MoveLostReport(lostReport, LostReportStatus.Matched, actorId, reason);
        }
    }

    /// <summary>
    /// Approval is the only path that closes anything: the item is handed over, the search is
    /// over, and every other open claim on that item is now moot.
    /// </summary>
    private async Task ApproveAsync(Claim claim, Guid staffId, string? reason, CancellationToken cancellationToken,
        bool tellOwnerWhereToCollect = true)
    {
        MoveClaim(claim, ClaimStatus.Approved, staffId, reason);

        // Approval reserves the item; collection returns it. Between the two the owner holds
        // a code and the item stays on the shelf as Claimed, so the desk can tell "decided"
        // from "gone" and the report's track does not say Returned before it is.
        claim.CollectionCode = await NextCollectionCodeAsync(cancellationToken);

        var foundReport = await _db.FoundReports
            .FirstOrDefaultAsync(f => f.Id == claim.FoundReportId, cancellationToken);

        if (foundReport is not null)
        {
            foundReport.Status = FoundReportStatus.Claimed;
            foundReport.UpdatedAt = DateTime.UtcNow;
        }

        if (tellOwnerWhereToCollect)
        _notifications.Queue(
            claim.StudentId,
            NotificationType.ClaimApproved,
            "Your claim was approved",
            reason ?? "The desk agrees the item is yours.",
            nameof(Claim),
            claim.Id);

        // Two notifications, because they answer two different questions: is it mine, and
        // where do I go. The second is the one someone re-reads on their way across campus.
        var storage = foundReport is null
            ? null
            : await _db.StorageLocations
                .AsNoTracking()
                .Where(l => l.Id == foundReport.StorageLocationId)
                .Select(l => new { l.Name, l.Building })
                .FirstOrDefaultAsync(cancellationToken);

        var where = storage is null
            ? "the desk"
            : storage.Building is null ? storage.Name : $"{storage.Name}, {storage.Building}";

        if (tellOwnerWhereToCollect)
        _notifications.Queue(
            claim.StudentId,
            NotificationType.CollectionInstructions,
            "Where to collect it",
            $"Go to {where} with your student ID and quote code {HandoverCodes.Display(claim.CollectionCode)}. It works once.",
            nameof(Claim),
            claim.Id);

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

            _notifications.Queue(
                rival.StudentId,
                NotificationType.ClaimRejected,
                "Your claim was closed",
                "Someone else proved the item was theirs. Your report is active again, so keep an eye out.",
                nameof(Claim),
                rival.Id);

            await ReopenLostReportIfNothingElsePendingAsync(rival, staffId, cancellationToken);
        }

        // The owner's other claims on the same report were for other candidate items. Theirs
        // has been found, so those can stop taking up a place in the queue.
        var siblings = await _db.Claims
            .Where(c => c.LostReportId == claim.LostReportId
                && c.Id != claim.Id
                && OpenStatuses.Contains(c.Status))
            .ToListAsync(cancellationToken);

        foreach (var sibling in siblings)
        {
            MoveClaim(sibling, ClaimStatus.Cancelled, staffId, "Another item was approved for this report.");
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

        // Still spoken for if another claim is open, an approved one is waiting to be collected,
        // or a finder's handover is on its way or already at a desk. Any of those means the
        // item may well be found; putting the notice back on the feed would say it is not.
        var stillOpen = await _db.Claims.AnyAsync(
            c => c.LostReportId == claim.LostReportId
                && c.Id != claim.Id
                && (OpenStatuses.Contains(c.Status) || (c.Status == ClaimStatus.Approved && c.CollectedAt == null)),
            cancellationToken)
            || await _db.LostReportFoundClaims.AnyAsync(
                h => h.LostReportId == claim.LostReportId
                    && (h.Status == HandoverStatus.AwaitingHandIn || h.Status == HandoverStatus.InCustody),
                cancellationToken);

        if (!stillOpen)
        {
            // A report made only to carry a claim was never a public notice - it closes with
            // the claim rather than appearing on the feed in the owner's name.
            if (lostReport.CreatedForClaim)
            {
                MoveLostReport(lostReport, LostReportStatus.Withdrawn, actorId, "The claim it was made for did not succeed.");
                lostReport.WithdrawnAt = DateTime.UtcNow;
                return;
            }
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
            if (!Enum.TryParse<ClaimStatus>(query.Status, ignoreCase: true, out var status) || !Enum.IsDefined(status))
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
                c.UpdatedAt,
                c.CollectedAt, c.LostReportId, c.FoundReportId,
                c.CustodyLocation == null ? (c.FoundReport.StorageLocation == null ? null : c.FoundReport.StorageLocation.Name) : c.CustodyLocation.Name,
                c.MatchSuggestion == null ? null : (decimal?)c.MatchSuggestion.MatchScore,
                c.Status.ToString()))
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
                    c.FoundReport.Status.ToString(),
                    c.FoundReport.StorageLocation == null ? null : c.FoundReport.StorageLocation.Name),
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
                    .Select(d => d.IsOverride && d.OverriddenByUser != null
                        ? d.OverriddenByUser.FullName
                        : d.DecidedByUser.FullName)
                    .FirstOrDefault(),
                c.ApprovalDecisions
                    .OrderByDescending(d => d.DecidedAt)
                    .Select(d => (DateTime?)d.DecidedAt)
                    .FirstOrDefault(),
                c.ApprovalDecisions
                    .OrderByDescending(d => d.DecidedAt)
                    .Select(d => d.IsOverride)
                    .FirstOrDefault(),
                c.CollectionCode,
                c.CollectedAt,
                c.CreatedAt,
                c.UpdatedAt,
                // Filled in only on the staff path - see ForStaffAsync.
                null, c.MatchSuggestionId, null, null, false))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundAppException($"Claim '{id}' was not found.");
}
