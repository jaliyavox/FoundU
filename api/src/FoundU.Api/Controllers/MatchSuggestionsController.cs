using FoundU.Api.Extensions;
using FoundU.Application.Abstractions;
using FoundU.Application.Auth;
using FoundU.Application.Common.Pagination;
using FoundU.Application.Matching.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoundU.Api.Controllers;

/// <summary>
/// The bridge between an item in storage and the student looking for it.
///
/// There is no browsable list of found items - that is how someone shops for something to
/// claim - so this is the controlled way a student learns that one specific item might be
/// theirs. Staff make the link by hand today; the Matching Agent writes the same rows later.
/// </summary>
[ApiController]
[Route("api/match-suggestions")]
[Authorize]
public class MatchSuggestionsController : ControllerBase
{
    private readonly IMatchSuggestionService _suggestions;

    public MatchSuggestionsController(IMatchSuggestionService suggestions)
    {
        _suggestions = suggestions;
    }

    [HttpPost]
    [Authorize(Policy = PolicyNames.Staff)]
    public async Task<ActionResult<MatchSuggestionDto>> Create(
        [FromBody] CreateMatchSuggestionRequest request,
        CancellationToken cancellationToken)
        => Ok(await _suggestions.CreateAsync(request, User.GetUserId(), cancellationToken));

    /// <summary>What the student sees on their own reports.</summary>
    [HttpGet("mine")]
    [Authorize(Policy = PolicyNames.Student)]
    public async Task<ActionResult<PagedResult<MatchSuggestionDto>>> Mine(
        [FromQuery] PaginationQuery query,
        CancellationToken cancellationToken)
        => Ok(await _suggestions.GetForStudentAsync(User.GetUserId(), query, cancellationToken));

    /// <summary>Everyone suggested for one item, for the staff working it.</summary>
    [HttpGet("for-item/{foundReportId:guid}")]
    [Authorize(Policy = PolicyNames.Staff)]
    public async Task<ActionResult<IReadOnlyList<MatchSuggestionDto>>> ForItem(
        Guid foundReportId,
        CancellationToken cancellationToken)
        => Ok(await _suggestions.GetForFoundReportAsync(foundReportId, cancellationToken));

    /// <summary>"That is not mine." Takes it off the student's list without opening a claim.</summary>
    [HttpPost("{id:guid}/dismiss")]
    [Authorize(Policy = PolicyNames.Student)]
    public async Task<ActionResult<MatchSuggestionDto>> Dismiss(
        Guid id,
        [FromBody] DismissMatchSuggestionRequest request,
        CancellationToken cancellationToken)
        => Ok(await _suggestions.DismissAsync(id, User.GetUserId(), request.Reason, cancellationToken));
}
