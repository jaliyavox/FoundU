using FoundU.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using FoundU.Domain.Common;
namespace FoundU.Domain.Entities;

/// <summary>
/// Identity user for FoundU. Inherits ASP.NET Core Identity's IdentityUser&lt;Guid&gt;, which
/// supplies Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed,
/// PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed,
/// TwoFactorEnabled, LockoutEnd, LockoutEnabled and AccessFailedCount out of the box.
///
/// Convention: UserName is always set equal to Email at registration time, so Identity's own
/// unique index on NormalizedUserName ("UserNameIndex") gives us case-insensitive email
/// uniqueness for free - there is no separate custom NormalizedEmail sync logic anymore.
/// </summary>
public class AppUser : IdentityUser<Guid>, ISoftDeletable
{
    public string FullName { get; set; } = default!;

    /// <summary>Role-based authorization source of truth (Student/Staff/Admin), embedded into the JWT as a role claim.</summary>
    public UserRole Role { get; set; }

    public string? StudentNumber { get; set; }

    public bool IsSuspended { get; set; }
    public string? SuspensionReason { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public Guid? SuspendedByUserId { get; set; }
    public AppUser? SuspendedByUser { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<LostReport> LostReports { get; set; } = new List<LostReport>();
    public ICollection<FoundReport> FoundReports { get; set; } = new List<FoundReport>();
    public ICollection<Claim> Claims { get; set; } = new List<Claim>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<StorageTransfer> StorageTransfers { get; set; } = new List<StorageTransfer>();
    public ICollection<ApprovalDecision> ApprovalDecisions { get; set; } = new List<ApprovalDecision>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
