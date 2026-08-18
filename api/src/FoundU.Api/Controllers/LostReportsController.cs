using FoundU.Api.Extensions;
using FoundU.Application.Abstractions;
using FoundU.Application.Auth;
using FoundU.Application.Common.Pagination;
using FoundU.Application.LostReports.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoundU.Api.Controllers;

/// <summary>
/// Lost items reported by students. Students act on their own reports; Staff/Admin can list
/// and read every report so they can work the desk.
/// </summary>
[ApiController]
[Route("api/lost-reports")]
[Authorize]
public class LostReportsController : ControllerBase
{
    private readonly ILostReportService _lostReports;

    public LostReportsController(ILostReportService lostReports)
    {
        _lostReports = lostReports;
    }

    [HttpPost]
    [Authorize(Policy = PolicyNames.Student)]
    public async Task<ActionResult<LostReportDetailDto>> Create(
        [FromBody] CreateLostReportRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _lostReports.CreateAsync(request, User.GetUserId(), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Staff/Admin view across every student's reports.</summary>
    [HttpGet]
    [Authorize(Policy = PolicyNames.Staff)]
    public async Task<ActionResult<PagedResult<LostReportListItemDto>>> Search(
        [FromQuery] LostReportQuery query,
        CancellationToken cancellationToken)
        => Ok(await _lostReports.SearchAsync(query, cancellationToken));

    /// <summary>The signed-in student's own reports.</summary>
    [HttpGet("mine")]
    [Authorize(Policy = PolicyNames.Student)]
    public async Task<ActionResult<PagedResult<LostReportListItemDto>>> Mine(
        [FromQuery] LostReportQuery query,
        CancellationToken cancellationToken)
        => Ok(await _lostReports.SearchForStudentAsync(User.GetUserId(), query, cancellationToken));

    /// <summary>Students may read only their own; staff may read any. Enforced in the service.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LostReportDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _lostReports.GetByIdAsync(id, User.GetUserId(), User.IsStaffOrAdmin(), cancellationToken));

    [HttpPost("{id:guid}/withdraw")]
    [Authorize(Policy = PolicyNames.Student)]
    public async Task<ActionResult<LostReportDetailDto>> Withdraw(
        Guid id,
        [FromBody] WithdrawLostReportRequest request,
        CancellationToken cancellationToken)
        => Ok(await _lostReports.WithdrawAsync(id, User.GetUserId(), request.Reason, cancellationToken));
}
