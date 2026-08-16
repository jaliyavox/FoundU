using System.Security.Claims;
using FoundU.Application.Abstractions;
using FoundU.Application.Auth;
using FoundU.Application.Claims.Dtos;
using FoundU.Application.Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoundU.Api.Controllers;

[ApiController]
[Route("api/claims")]
[Authorize(Policy = PolicyNames.Student)]
public class ClaimsController : ControllerBase
{
    private readonly IClaimService _claimService;

    public ClaimsController(IClaimService claimService)
    {
        _claimService = claimService;
    }

    [HttpPost]
    public async Task<ActionResult<ClaimResponse>> Create([FromBody] CreateClaimRequest request)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdValue, out var authenticatedUserId))
        {
            throw new UnauthorizedAppException("The access token does not identify a valid user.");
        }

        var result = await _claimService.CreateAsync(authenticatedUserId, request);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
