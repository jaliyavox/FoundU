using FoundU.Api.Extensions;
using FoundU.Application.Abstractions;
using FoundU.Application.Auth;
using FoundU.Application.Common.Pagination;
using FoundU.Application.FoundReports.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoundU.Api.Controllers;

/// <summary>
/// Found items logged at the desk. Staff/Admin only - these responses include
/// PrivateVerificationDetails, the hidden evidence a claimant must be able to describe.
/// </summary>
[ApiController]
[Route("api/found-reports")]
[Authorize(Policy = PolicyNames.Staff)]
public class FoundReportsController : ControllerBase
{
    private readonly IFoundReportService _foundReports;

    public FoundReportsController(IFoundReportService foundReports)
    {
        _foundReports = foundReports;
    }

    [HttpPost]
    public async Task<ActionResult<FoundReportDetailDto>> Create(
        [FromBody] CreateFoundReportRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _foundReports.CreateAsync(request, User.GetUserId(), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Paged, filterable items table. See /docs/api/conventions.md "Pagination".</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<FoundReportListItemDto>>> Search(
        [FromQuery] FoundReportQuery query,
        CancellationToken cancellationToken)
        => Ok(await _foundReports.SearchAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FoundReportDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _foundReports.GetByIdAsync(id, cancellationToken));
}
