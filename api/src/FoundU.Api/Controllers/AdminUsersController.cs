using FoundU.Api.Extensions;
using FoundU.Application.Abstractions;
using FoundU.Application.Admin.Dtos;
using FoundU.Application.Auth;
using FoundU.Application.Common.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoundU.Api.Controllers;

/// <summary>
/// Account administration. Admin policy only - the Staff policy includes Admin, but not the
/// other way round, so staff cannot reach these.
/// </summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = PolicyNames.Admin)]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _adminUsers;

    public AdminUsersController(IAdminUserService adminUsers)
    {
        _adminUsers = adminUsers;
    }

    /// <summary>Headline counts for the dashboard cards.</summary>
    [HttpGet("stats")]
    public async Task<ActionResult<AdminUserStatsDto>> Stats(CancellationToken cancellationToken)
        => Ok(await _adminUsers.GetStatsAsync(cancellationToken));

    /// <summary>Paged, searchable user list. See /docs/api/conventions.md "Pagination".</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminUserListItemDto>>> Search(
        [FromQuery] AdminUserQuery query,
        CancellationToken cancellationToken)
        => Ok(await _adminUsers.SearchAsync(query, cancellationToken));

    /// <summary>
    /// Suspends an account, blocking login and refresh immediately. The acting admin comes
    /// from the token, never the request body.
    /// </summary>
    [HttpPost("{id:guid}/suspend")]
    public async Task<ActionResult<AdminUserListItemDto>> Suspend(
        Guid id,
        [FromBody] SuspendUserRequest request,
        CancellationToken cancellationToken)
        => Ok(await _adminUsers.SuspendAsync(id, User.GetUserId(), request.Reason, cancellationToken));

    [HttpPost("{id:guid}/reinstate")]
    public async Task<ActionResult<AdminUserListItemDto>> Reinstate(Guid id, CancellationToken cancellationToken)
        => Ok(await _adminUsers.ReinstateAsync(id, cancellationToken));
}
