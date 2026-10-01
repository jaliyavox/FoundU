using FoundU.Api.Extensions;
using FoundU.Application.Abstractions;
using FoundU.Application.Common.Pagination;
using FoundU.Application.Support.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoundU.Api.Controllers;

/// <summary>
/// Support tickets from the asker's side: open one, read your own, write on it.
///
/// Staff use the same ticket through <c>/api/admin/support</c>; the difference is what each
/// side may do, not a second copy of the conversation.
/// </summary>
[ApiController]
[Route("api/support/tickets")]
[Authorize]
public class SupportController : ControllerBase
{
    private readonly ISupportService _support;

    public SupportController(ISupportService support)
    {
        _support = support;
    }

    [HttpPost]
    public async Task<ActionResult<SupportTicketDetailDto>> Create(
        [FromBody] CreateSupportTicketRequest request,
        CancellationToken cancellationToken)
    {
        var ticket = await _support.CreateAsync(request, User.GetUserId(), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<SupportTicketListItemDto>>> Mine(
        [FromQuery] SupportTicketQuery query,
        CancellationToken cancellationToken)
        => Ok(await _support.GetMineAsync(User.GetUserId(), query, cancellationToken));

    /// <summary>The person who raised it, or any staff member. Reading marks the other side's messages seen.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SupportTicketDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _support.GetByIdAsync(id, User.GetUserId(), User.IsStaffOrAdmin(), cancellationToken));

    [HttpPost("{id:guid}/messages")]
    public async Task<ActionResult<SupportTicketDetailDto>> Reply(
        Guid id,
        [FromBody] SupportTicketReplyRequest request,
        CancellationToken cancellationToken)
        => Ok(await _support.ReplyAsync(id, User.GetUserId(), User.IsStaffOrAdmin(), request.Body, cancellationToken));
}
