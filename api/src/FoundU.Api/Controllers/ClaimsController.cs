using FoundU.Api.Extensions;
using FoundU.Application.Abstractions;
using FoundU.Application.Auth;
using FoundU.Application.Claims.Dtos;
using FoundU.Application.Common.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoundU.Api.Controllers;

/// <summary>
/// Claims on found items. Students act on their own claims; Staff/Admin work the queue and
/// make the decision. Nothing reachable from here exposes the hidden ownership evidence -
/// the questions are written from it, but the text itself never leaves the staff side.
/// </summary>
[ApiController]
[Route("api/claims")]
[Authorize]
public class ClaimsController : ControllerBase
{
    private readonly IClaimService _claims;

    public ClaimsController(IClaimService claims)
    {
        _claims = claims;
    }

    [HttpPost]
    [Authorize(Policy = PolicyNames.Student)]
    public async Task<ActionResult<ClaimDetailDto>> Create(
        [FromBody] CreateClaimRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _claims.CreateAsync(request, User.GetUserId(), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>The staff review queue, oldest first - longest wait is worked next.</summary>
    [HttpGet]
    [Authorize(Policy = PolicyNames.Staff)]
    public async Task<ActionResult<PagedResult<ClaimListItemDto>>> Search(
        [FromQuery] ClaimQuery query,
        CancellationToken cancellationToken)
        => Ok(await _claims.SearchAsync(query, cancellationToken));

    /// <summary>The signed-in student's own claims.</summary>
    [HttpGet("mine")]
    [Authorize(Policy = PolicyNames.Student)]
    public async Task<ActionResult<PagedResult<ClaimListItemDto>>> Mine(
        [FromQuery] ClaimQuery query,
        CancellationToken cancellationToken)
        => Ok(await _claims.SearchForStudentAsync(User.GetUserId(), query, cancellationToken));

    /// <summary>Students may read only their own; staff may read any. Enforced in the service.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClaimDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _claims.GetByIdAsync(id, User.GetUserId(), User.IsStaffOrAdmin(), cancellationToken));

    /// <summary>
    /// Staff writing the questions the claimant must answer, from the item's hidden evidence.
    /// The Verification Agent takes this over in Step 12.
    /// </summary>
    [HttpPost("{id:guid}/questions")]
    [Authorize(Policy = PolicyNames.Staff)]
    public async Task<ActionResult<ClaimDetailDto>> AddQuestions(
        Guid id,
        [FromBody] AddVerificationQuestionsRequest request,
        CancellationToken cancellationToken)
        => Ok(await _claims.AddQuestionsAsync(id, User.GetUserId(), request, cancellationToken));

    /// <summary>The student answering. Every outstanding question must be answered at once.</summary>
    [HttpPost("{id:guid}/answers")]
    [Authorize(Policy = PolicyNames.Student)]
    public async Task<ActionResult<ClaimDetailDto>> SubmitAnswers(
        Guid id,
        [FromBody] SubmitClaimAnswersRequest request,
        CancellationToken cancellationToken)
        => Ok(await _claims.SubmitAnswersAsync(id, User.GetUserId(), request, cancellationToken));

    /// <summary>Approve, reject, or send the claim back for another attempt.</summary>
    [HttpPost("{id:guid}/decision")]
    [Authorize(Policy = PolicyNames.Staff)]
    public async Task<ActionResult<ClaimDetailDto>> Decide(
        Guid id,
        [FromBody] ClaimDecisionRequest request,
        CancellationToken cancellationToken)
        => Ok(await _claims.DecideAsync(id, User.GetUserId(), request, cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = PolicyNames.Student)]
    public async Task<ActionResult<ClaimDetailDto>> Cancel(
        Guid id,
        [FromBody] CancelClaimRequest request,
        CancellationToken cancellationToken)
        => Ok(await _claims.CancelAsync(id, User.GetUserId(), request.Reason, cancellationToken));
}

/// <summary>Optional note from the student giving up on a claim.</summary>
public record CancelClaimRequest(string? Reason);
