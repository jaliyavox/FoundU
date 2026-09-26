using FoundU.Api.Extensions;
using FoundU.Application.Abstractions;
using FoundU.Application.Auth;
using FoundU.Application.Common.Pagination;
using FoundU.Application.Support.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoundU.Api.Controllers;

/// <summary>
/// The support queue, for the people who answer it. Reading and replying to a single ticket
/// happens through <c>/api/support/tickets/{id}</c> - this is the queue and the levers.
/// </summary>
[ApiController]
[Route("api/admin/support")]
[Authorize(Policy = PolicyNames.Staff)]
public class AdminSupportController : ControllerBase
{
    private readonly ISupportService _support;

    public AdminSupportController(ISupportService support)
    {
        _support = support;
    }

    [HttpGet("tickets")]
    public async Task<ActionResult<PagedResult<SupportTicketListItemDto>>> Queue(
        [FromQuery] SupportTicketQuery query,
        CancellationToken cancellationToken)
        => Ok(await _support.SearchAsync(User.GetUserId(), query, cancellationToken));

    [HttpGet("stats")]
    public async Task<ActionResult<SupportQueueStatsDto>> Stats(CancellationToken cancellationToken)
        => Ok(await _support.GetQueueStatsAsync(cancellationToken));

    /// <summary>Status and assignment. The conversation itself is written through the shared endpoint.</summary>
    [HttpPut("tickets/{id:guid}")]
    public async Task<ActionResult<SupportTicketDetailDto>> Update(
        Guid id,
        [FromBody] UpdateSupportTicketRequest request,
        CancellationToken cancellationToken)
        => Ok(await _support.UpdateAsync(id, User.GetUserId(), request, cancellationToken));
}
