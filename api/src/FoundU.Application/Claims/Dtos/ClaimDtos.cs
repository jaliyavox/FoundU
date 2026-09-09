using FoundU.Application.Common.Pagination;
using FoundU.Application.FoundReports.Dtos;

namespace FoundU.Application.Claims.Dtos;

/// <summary>
/// A student asserting that a specific found item is the thing they reported lost.
///
/// Both ids are required: the claim is a link between the student's own lost report and one
/// found item, and both ends are checked against the caller before anything is written.
/// </summary>
public record CreateClaimRequest(Guid LostReportId, Guid FoundReportId);

/// <summary>
/// A verification question and, once given, the student's answer.
///
/// <c>IsCorrect</c> is deliberately absent: the deterministic check belongs to the
/// Verification Agent (Step 12), and until then telling a claimant which answers passed
/// would hand a fraudulent one the feedback loop they need to guess the rest.
/// </summary>
public record ClaimQuestionDto(
    Guid Id,
    string QuestionText,
    string? AnswerText,
    DateTime? AnsweredAt);

/// <summary>Row in either review queue - the student's own claims, or the staff queue.</summary>
public record ClaimListItemDto(
    Guid Id,
    string Status,
    string CategoryName,
    string ItemTypeName,
    string StudentName,
    /// <summary>Questions still waiting on an answer - what the student has to do next.</summary>
    int UnansweredQuestionCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// Full claim. The found item is projected through <see cref="FoundReportSummaryDto"/>, which
/// is the student-safe shape - it carries no PrivateVerificationDetails, and that is the whole
/// basis of verification: the claimant must describe the hidden detail from memory.
/// </summary>
public record ClaimDetailDto(
    Guid Id,
    string Status,
    Guid StudentId,
    string StudentName,
    Guid LostReportId,
    string LostReportDescription,
    FoundReportSummaryDto FoundItem,
    IReadOnlyList<ClaimQuestionDto> Questions,
    /// <summary>The most recent decision, if a staff member has made one.</summary>
    string? Decision,
    string? DecisionReason,
    string? DecidedByName,
    DateTime? DecidedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Staff writing the questions a claimant must answer.</summary>
public record AddVerificationQuestionsRequest(IReadOnlyList<string> Questions);

public record ClaimAnswerInput(Guid QuestionId, string AnswerText);

/// <summary>The student answering. Every outstanding question must be answered at once.</summary>
public record SubmitClaimAnswersRequest(IReadOnlyList<ClaimAnswerInput> Answers);

/// <summary>Staff approving, rejecting, or sending a claim back for another attempt.</summary>
public record ClaimDecisionRequest(string Decision, string? Reason);

public class ClaimQuery : PaginationQuery
{
    /// <summary>Filter by ClaimStatus name (Pending, WaitingForAnswer, UnderReview, ...).</summary>
    public string? Status { get; set; }
}
