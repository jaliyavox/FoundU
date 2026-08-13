using System.Reflection;
using FoundU.Domain.Common;
using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Infrastructure.Persistence;

public class FoundUDbContext : DbContext
{
    public FoundUDbContext(DbContextOptions<FoundUDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ItemType> ItemTypes => Set<ItemType>();
    public DbSet<CampusLocation> CampusLocations => Set<CampusLocation>();

    public DbSet<LostReport> LostReports => Set<LostReport>();
    public DbSet<LostItemPhoto> LostItemPhotos => Set<LostItemPhoto>();
    public DbSet<LostReportStatusHistory> LostReportStatusHistories => Set<LostReportStatusHistory>();

    public DbSet<FoundReport> FoundReports => Set<FoundReport>();
    public DbSet<FoundItemPhoto> FoundItemPhotos => Set<FoundItemPhoto>();
    public DbSet<FoundReportStatusHistory> FoundReportStatusHistories => Set<FoundReportStatusHistory>();

    public DbSet<StorageLocation> StorageLocations => Set<StorageLocation>();
    public DbSet<StorageTransfer> StorageTransfers => Set<StorageTransfer>();

    public DbSet<MatchSuggestion> MatchSuggestions => Set<MatchSuggestion>();
    public DbSet<MatchStatusHistory> MatchStatusHistories => Set<MatchStatusHistory>();

    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<VerificationQuestion> VerificationQuestions => Set<VerificationQuestion>();
    public DbSet<ClaimAnswer> ClaimAnswers => Set<ClaimAnswer>();
    public DbSet<ClaimStatusHistory> ClaimStatusHistories => Set<ClaimStatusHistory>();
    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();
    public DbSet<AgentStep> AgentSteps => Set<AgentStep>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Applies every IEntityTypeConfiguration<T> in this assembly - one file per entity,
        // Fluent API only, no data annotations.
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    public override int SaveChanges()
    {
        ApplyAuditTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Automatically stamps CreatedAt/UpdatedAt (UTC) on every tracked BaseEntity, and converts
    /// hard deletes into soft deletes for entities implementing ISoftDeletable.
    /// </summary>
    private void ApplyAuditTimestamps()
    {
        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = utcNow;
                    entry.Entity.UpdatedAt = utcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = utcNow;
                    break;
                case EntityState.Deleted when entry.Entity is ISoftDeletable softDeletable:
                    // Convert physical delete into a soft delete.
                    entry.State = EntityState.Modified;
                    softDeletable.IsDeleted = true;
                    softDeletable.DeletedAt = utcNow;
                    entry.Entity.UpdatedAt = utcNow;
                    break;
            }

            // Keep NormalizedEmail in sync whenever an AppUser is inserted or its Email changes,
            // so the case-insensitive unique index always reflects the current Email value.
            if (entry.Entity is AppUser user
                && (entry.State == EntityState.Added || entry.State == EntityState.Modified))
            {
                user.NormalizedEmail = user.Email.Trim().ToUpperInvariant();
            }
        }
    }
}
