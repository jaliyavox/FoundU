using FoundU.Application.Abstractions;
using FoundU.Application.Admin.Dtos;
using FoundU.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoundU.Api.Controllers;

/// <summary>The admin panel's front page: what needs a person today.</summary>
[ApiController]
[Route("api/admin/overview")]
[Authorize(Policy = PolicyNames.Staff)]
public class AdminOverviewController : ControllerBase
{
    private readonly IAdminOverviewService _overview;

    public AdminOverviewController(IAdminOverviewService overview)
    {
        _overview = overview;
    }

    [HttpGet]
    public async Task<ActionResult<AdminOverviewDto>> Get(CancellationToken cancellationToken)
        => Ok(await _overview.GetAsync(cancellationToken));
}
