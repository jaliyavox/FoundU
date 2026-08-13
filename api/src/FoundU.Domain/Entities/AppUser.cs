using FoundU.Domain.Common;
using FoundU.Domain.Enums;

namespace FoundU.Domain.Entities;

public class AppUser : BaseEntity, ISoftDeletable
{
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;

    /// <summary>
    /// Uppercased, trimmed copy of Email used for case-insensitive uniqueness
    /// (so "remo@email.com" and "Remo@email.com" cannot both register). Kept in sync
    /// automatically by FoundUDbContext.SaveChanges - do not set this manually.
    /// </summary>
    public string NormalizedEmail { get; set; } = default!;

    public string PasswordHash { get; set; } = default!;
    public UserRole Role { get; set; }

    public string? StudentNumber { get; set; }
    public string? PhoneNumber { get; set; }

    public bool IsSuspended { get; set; }
    public string? SuspensionReason { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public Guid? SuspendedByUserId { get; set; }
    public AppUser? SuspendedByUser { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public ICollection<LostReport> LostReports { get; set; } = new List<LostReport>();
    public ICollection<FoundReport> FoundReports { get; set; } = new List<FoundReport>();
    public ICollection<Claim> Claims { get; set; } = new List<Claim>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<StorageTransfer> StorageTransfers { get; set; } = new List<StorageTransfer>();
    public ICollection<ApprovalDecision> ApprovalDecisions { get; set; } = new List<ApprovalDecision>();
}
