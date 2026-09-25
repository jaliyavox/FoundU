using FoundU.Application.Admin.Dtos;
using FoundU.Application.Common.Pagination;

namespace FoundU.Application.Abstractions;

/// <summary>
/// Administrator-only account management. Suspension is a real lockout, not a flag:
/// AuthService rejects both login and token refresh for a suspended account.
/// </summary>
public interface IAdminUserService
{
    Task<AdminUserStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<AdminUserListItemDto>> SearchAsync(AdminUserQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Suspends an account. <paramref name="actingAdminId"/> is recorded against it, and is
    /// also what stops an admin suspending themselves.
    /// </summary>
    Task<AdminUserListItemDto> SuspendAsync(Guid userId, Guid actingAdminId, string reason, CancellationToken cancellationToken = default);

    Task<AdminUserListItemDto> ReinstateAsync(Guid userId, CancellationToken cancellationToken = default);
}
