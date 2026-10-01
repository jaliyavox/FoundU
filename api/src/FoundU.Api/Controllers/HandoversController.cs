using FoundU.Api.Extensions;
using FoundU.Application.Abstractions;
using FoundU.Application.Auth;
using FoundU.Application.Handovers.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoundU.Api.Controllers;

/// <summary>
/// The desk's side of a handover: type the code in, take the item, release it to its owner.
///
/// Codes are typed in here, never read out - no response on this controller carries one.
/// </summary>
[ApiController]
[Route("api/handovers")]
[Authorize(Policy = PolicyNames.Staff)]
public class HandoversController : ControllerBase
{
    private readonly IHandoverService _handovers;

    public HandoversController(IHandoverService handovers)
    {
        _handovers = handovers;
    }

    /// <summary>What is this code, and what should the desk do with it?</summary>
    [HttpGet("by-code/{code}")]
    public async Task<ActionResult<HandoverLookupDto>> Lookup(string code, CancellationToken cancellationToken)
        => Ok(await _handovers.LookupAsync(code, cancellationToken));

    /// <summary>The finder is here with the item.</summary>
    [HttpPost("by-code/{code}/receive")]
    public async Task<ActionResult<HandoverLookupDto>> Receive(
        string code,
        [FromBody] ReceiveHandoverRequest request,
        CancellationToken cancellationToken)
        => Ok(await _handovers.ReceiveAsync(code, User.GetUserId(), request, cancellationToken));

    /// <summary>The owner is here to collect. The desk confirms it checked who they are.</summary>
    [HttpPost("by-code/{code}/release")]
    public async Task<ActionResult<HandoverLookupDto>> Release(
        string code,
        [FromBody] ReleaseHandoverRequest request,
        CancellationToken cancellationToken)
        => Ok(await _handovers.ReleaseAsync(code, User.GetUserId(), request, cancellationToken));
}
