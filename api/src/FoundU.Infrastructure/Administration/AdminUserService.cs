using FoundU.Application.Abstractions;
using FoundU.Application.Admin.Dtos;
using FoundU.Application.Common.Exceptions;
using FoundU.Application.Common.Pagination;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Infrastructure.Administration;

public class AdminUserService : IAdminUserService
{
    private readonly FoundUDbContext _db;

    public AdminUserService(FoundUDbContext db)
    {
        _db = db;
    }

    public async Task<AdminUserStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

        // A single grouped round-trip rather than six counts.
        return await _db.Users
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new AdminUserStatsDto(
                g.Count(),
                g.Count(u => u.Role == UserRole.Student),
                g.Count(u => u.Role == UserRole.Staff),
                g.Count(u => u.Role == UserRole.Admin),
                g.Count(u => u.IsSuspended),
                g.Count(u => u.CreatedAt >= thirtyDaysAgo)))
            .FirstOrDefaultAsync(cancellationToken)
            // No users at all still needs a shape to render.
            ?? new AdminUserStatsDto(0, 0, 0, 0, 0, 0);
    }

    public async Task<PagedResult<AdminUserListItemDto>> SearchAsync(
        AdminUserQuery query,
        CancellationToken cancellationToken = default)
    {
        var users = _db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            if (!Enum.TryParse<UserRole>(query.Role, ignoreCase: true, out var role))
            {
                throw new ValidationAppException(nameof(query.Role),
                    $"Unknown role '{query.Role}'. Expected one of: {string.Join(", ", Enum.GetNames<UserRole>())}.");
            }

            users = users.Where(u => u.Role == role);
        }

        if (query.IsSuspended is { } suspended)
        {
            users = users.Where(u => u.IsSuspended == suspended);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{query.Search.Trim()}%";

            users = users.Where(u =>
                EF.Functions.ILike(u.FullName, term) ||
                EF.Functions.ILike(u.Email!, term) ||
                (u.StudentNumber != null && EF.Functions.ILike(u.StudentNumber, term)));
        }

        users = ApplySort(users, query);

        var totalCount = await users.CountAsync(cancellationToken);

        var items = await users
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(u => new AdminUserListItemDto(
                u.Id,
                u.FullName,
                u.Email!,
                u.Role.ToString(),
                u.StudentNumber,
                u.IsSuspended,
                u.SuspensionReason,
                u.SuspendedAt,
                u.SuspendedByUser != null ? u.SuspendedByUser.FullName : null,
                u.LostReports.Count,
                u.CreatedAt))
            .ToListAsync(cancellationToken);

        return PagedResult<AdminUserListItemDto>.Create(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<AdminUserListItemDto> SuspendAsync(
        Guid userId,
        Guid actingAdminId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundAppException($"User '{userId}' was not found.");

        // Locking yourself out is unrecoverable without database access.
        if (user.Id == actingAdminId)
        {
            throw new ValidationAppException(nameof(userId), "You cannot suspend your own account.");
        }

        // Otherwise two administrators can lock each other out of the system.
        if (user.Role == UserRole.Admin)
        {
            throw new ForbiddenAppException(
                "Administrator accounts cannot be suspended. Change the role first if this is intended.");
        }

        if (user.IsSuspended)
        {
            throw new ConflictAppException("This account is already suspended.");
        }

        user.IsSuspended = true;
        user.SuspensionReason = reason.Trim();
        user.SuspendedAt = DateTime.UtcNow;
        user.SuspendedByUserId = actingAdminId;
        user.UpdatedAt = DateTime.UtcNow;

        // Existing refresh tokens would otherwise keep the session alive until they expire.
        await RevokeActiveTokensAsync(user.Id, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        return await LoadAsync(user.Id, cancellationToken);
    }

    public async Task<AdminUserListItemDto> ReinstateAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundAppException($"User '{userId}' was not found.");

        if (!user.IsSuspended)
        {
            throw new ConflictAppException("This account is not suspended.");
        }

        user.IsSuspended = false;
        user.SuspensionReason = null;
        user.SuspendedAt = null;
        user.SuspendedByUserId = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return await LoadAsync(user.Id, cancellationToken);
    }

    /// <summary>
    /// Suspension blocks login and refresh, but an access token already in the wild stays
    /// valid until it expires. Revoking the refresh tokens caps that at the access token's
    /// lifetime instead of the refresh token's.
    /// </summary>
    private async Task RevokeActiveTokensAsync(Guid userId, CancellationToken cancellationToken)
    {
        var active = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in active)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task<AdminUserListItemDto> LoadAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new AdminUserListItemDto(
                u.Id,
                u.FullName,
                u.Email!,
                u.Role.ToString(),
                u.StudentNumber,
                u.IsSuspended,
                u.SuspensionReason,
                u.SuspendedAt,
                u.SuspendedByUser != null ? u.SuspendedByUser.FullName : null,
                u.LostReports.Count,
                u.CreatedAt))
            .FirstAsync(cancellationToken);
    }

    /// <summary>Allow-listed sort columns only - never interpolate a caller-supplied name.</summary>
    private static IQueryable<AppUser> ApplySort(IQueryable<AppUser> users, AdminUserQuery query)
    {
        var descending = string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return query.SortBy?.ToLowerInvariant() switch
        {
            "name" => descending ? users.OrderByDescending(u => u.FullName) : users.OrderBy(u => u.FullName),
            "email" => descending ? users.OrderByDescending(u => u.Email) : users.OrderBy(u => u.Email),
            "role" => descending ? users.OrderByDescending(u => u.Role) : users.OrderBy(u => u.Role),
            "created" => descending ? users.OrderByDescending(u => u.CreatedAt) : users.OrderBy(u => u.CreatedAt),
            // Newest sign-ups first is the useful default for a user list.
            _ => users.OrderByDescending(u => u.CreatedAt),
        };
    }
}
