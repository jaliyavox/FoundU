using FoundU.Api.Extensions;
using FoundU.Application.Abstractions;
using FoundU.Application.Auth;
using FoundU.Application.Common.Pagination;
using FoundU.Application.FoundReports.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoundU.Api.Controllers;

/// <summary>
/// "I found something" - a student's post before the item reaches a desk. Public to read,
/// signed-in to write, staff to confirm. Nothing here can be claimed; confirming is what
/// makes it claimable.
/// </summary>
[ApiController]
[Route("api/found-posts")]
[Authorize]
public class FoundPostsController : ControllerBase
{
    private readonly IFoundPostService _posts;

    public FoundPostsController(IFoundPostService posts)
    {
        _posts = posts;
    }

    /// <summary>Any signed-in user can post something they found - staff included.</summary>
    [HttpPost]
    public async Task<ActionResult<FoundPostFeedItemDto>> Post(
        [FromBody] CreateFoundPostRequest request,
        CancellationToken cancellationToken)
        => Ok(await _posts.PostAsync(request, User.GetUserId(), cancellationToken));

    /// <summary>Public. A token, if sent, marks the caller's own posts and carries their code.</summary>
    [HttpGet("feed")]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<FoundPostFeedItemDto>>> Feed(
        [FromQuery] FoundPostQuery query,
        CancellationToken cancellationToken)
        => Ok(await _posts.GetFeedAsync(query, User.GetUserIdOrNull(), cancellationToken));

    [HttpGet("mine")]
    public async Task<ActionResult<PagedResult<FoundPostFeedItemDto>>> Mine(
        [FromQuery] PaginationQuery query,
        CancellationToken cancellationToken)
        => Ok(await _posts.GetMineAsync(User.GetUserId(), query, cancellationToken));

    /// <summary>"That is mine." Links the post to one of the caller's reports and tells the finder to hand it in.</summary>
    [HttpPost("{id:guid}/recognise")]
    [Authorize(Policy = PolicyNames.Student)]
    public async Task<ActionResult<FoundPostFeedItemDto>> Recognise(
        Guid id,
        [FromBody] RecogniseFoundPostRequest request,
        CancellationToken cancellationToken)
        => Ok(await _posts.RecogniseAsync(id, User.GetUserId(), request, cancellationToken));

    [HttpPost("{id:guid}/withdraw")]
    public async Task<ActionResult<FoundPostFeedItemDto>> Withdraw(
        Guid id,
        [FromBody] WithdrawFoundPostRequest request,
        CancellationToken cancellationToken)
        => Ok(await _posts.WithdrawAsync(id, User.GetUserId(), request.Reason, cancellationToken));

    /// <summary>The desk pulling a post up by the code the finder quotes.</summary>
    [HttpGet("by-code/{code}")]
    [Authorize(Policy = PolicyNames.Staff)]
    public async Task<ActionResult<FoundReportDetailDto>> ByCode(string code, CancellationToken cancellationToken)
        => Ok(await _posts.GetByHandInCodeAsync(code, cancellationToken));

    /// <summary>The desk confirming a post: in storage, hidden detail written, claimable from here.</summary>
    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = PolicyNames.Staff)]
    public async Task<ActionResult<FoundReportDetailDto>> Confirm(
        Guid id,
        [FromBody] ConfirmFoundPostRequest request,
        CancellationToken cancellationToken)
        => Ok(await _posts.ConfirmAsync(id, User.GetUserId(), request, cancellationToken));
}
