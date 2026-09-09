using FoundU.Application.Claims.Dtos;
using FoundU.Application.Common.Pagination;

namespace FoundU.Application.Abstractions;

/// <summary>
/// Claims: a student asserting that a found item is theirs, and staff deciding.
///
/// Two audiences, one entity. Students act only on their own claims and never see the hidden
/// ownership evidence; staff see the queue and make the decision. Every method that a student
/// can reach takes the caller's id so ownership is enforced in one place rather than trusted
/// from the request.
/// </summary>
public interface IClaimService
{
    Task<ClaimDetailDto> CreateAsync(CreateClaimRequest request, Guid studentId, CancellationToken cancellationToken = default);

    /// <summary>The signed-in student's own claims.</summary>
    Task<PagedResult<ClaimListItemDto>> SearchForStudentAsync(Guid studentId, ClaimQuery query, CancellationToken cancellationToken = default);

    /// <summary>The staff review queue, across every student.</summary>
    Task<PagedResult<ClaimListItemDto>> SearchAsync(ClaimQuery query, CancellationToken cancellationToken = default);

    /// <summary>Students may read only their own; staff may read any. Enforced in the service.</summary>
    Task<ClaimDetailDto> GetByIdAsync(Guid id, Guid requesterId, bool requesterIsStaff, CancellationToken cancellationToken = default);

    /// <summary>
    /// Staff adds the questions the claimant must answer. Written from the found item's hidden
    /// evidence - the Verification Agent generates these once Step 12 lands.
    /// </summary>
    Task<ClaimDetailDto> AddQuestionsAsync(Guid claimId, Guid staffId, AddVerificationQuestionsRequest request, CancellationToken cancellationToken = default);

    /// <summary>The student answering every outstanding question, which sends the claim to review.</summary>
    Task<ClaimDetailDto> SubmitAnswersAsync(Guid claimId, Guid studentId, SubmitClaimAnswersRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Staff approving, rejecting, or requesting a revision. Approval is what resolves the
    /// lost report and marks the item returned, so it is the only path that closes anything.
    /// </summary>
    Task<ClaimDetailDto> DecideAsync(Guid claimId, Guid staffId, ClaimDecisionRequest request, CancellationToken cancellationToken = default);

    /// <summary>The student giving up on their own claim. Nothing is deleted - it is recorded.</summary>
    Task<ClaimDetailDto> CancelAsync(Guid claimId, Guid studentId, string? reason, CancellationToken cancellationToken = default);
}
