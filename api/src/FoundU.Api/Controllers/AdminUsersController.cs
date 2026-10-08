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

    /// <summary>
    /// How Staff and Admin accounts are made: the person registers, then an admin changes
    /// their role here. Their sessions end so the new role applies at once.
    /// </summary>
    [HttpPut("{id:guid}/role")]
    public async Task<ActionResult<AdminUserListItemDto>> ChangeRole(
        Guid id,
        [FromBody] ChangeUserRoleRequest request,
        CancellationToken cancellationToken)
        => Ok(await _adminUsers.ChangeRoleAsync(id, User.GetUserId(), request.Role, cancellationToken));

    /// <summary>
    /// Deletes an account: open reports, posts and handovers are closed, personal details are
    /// erased and the person can no longer sign in. Never the acting admin, never another admin.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] IAccountDeletionService accounts,
        CancellationToken cancellationToken)
    {
        await accounts.DeleteAsync(id, User.GetUserId(), cancellationToken);
        return NoContent();
    }
}
