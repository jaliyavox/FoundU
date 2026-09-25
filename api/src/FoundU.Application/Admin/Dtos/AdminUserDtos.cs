using FoundU.Application.Common.Pagination;

namespace FoundU.Application.Admin.Dtos;

/// <summary>
/// A user as an administrator sees them. Includes email and student number - unlike the
/// public feed projection - because managing an account requires identifying it.
/// Never includes PasswordHash, SecurityStamp or any other Identity internal.
/// </summary>
public record AdminUserListItemDto(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    string? StudentNumber,
    bool IsSuspended,
    string? SuspensionReason,
    DateTime? SuspendedAt,
    string? SuspendedByName,
    int LostReportCount,
    DateTime CreatedAt);

public record SuspendUserRequest(string Reason);

/// <summary>Filters for the admin users table, on top of the standard pagination contract.</summary>
public class AdminUserQuery : PaginationQuery
{
    /// <summary>Filter by UserRole name (Student, Staff, Admin).</summary>
    public string? Role { get; set; }

    /// <summary>true = only suspended, false = only active, null = both.</summary>
    public bool? IsSuspended { get; set; }
}

/// <summary>Headline counts for the admin dashboard. One query rather than four filtered calls.</summary>
public record AdminUserStatsDto(
    int TotalUsers,
    int Students,
    int Staff,
    int Admins,
    int Suspended,
    int JoinedLast30Days);
