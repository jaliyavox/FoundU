using FoundU.Application.Abstractions;
using FoundU.Application.Admin.Dtos;
using FoundU.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoundU.Api.Controllers;

/// <summary>Admin-only aggregates for the analytics screen. Real counts, nothing estimated.</summary>
[ApiController]
[Route("api/admin/analytics")]
[Authorize(Policy = PolicyNames.Admin)]
public class AdminAnalyticsController : ControllerBase
{
    private readonly IAdminAnalyticsService _analytics;

    public AdminAnalyticsController(IAdminAnalyticsService analytics)
    {
        _analytics = analytics;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<AnalyticsOverviewDto>> Overview(CancellationToken cancellationToken)
        => Ok(await _analytics.GetOverviewAsync(cancellationToken));
}
