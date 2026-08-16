using System.Reflection;
using FoundU.Domain.Common;
using FoundU.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Infrastructure.Persistence;

/// <summary>
/// Inherits IdentityUserContext so UserManager/SignInManager work against AppUser without also
/// creating Identity's separate role store. FoundU's fixed AppUser.Role value is the single
/// authorization source of truth and is embedded into each JWT as a role claim.
/// </summary>
public class FoundUDbContext : IdentityUserContext<AppUser, Guid>
{
    public FoundUDbContext(DbContextOptions<FoundUDbContext> options) : base(options)
    {
    }

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

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Sets up Identity's user schema (users, claims, logins, and tokens) first.
        base.OnModelCreating(modelBuilder);

        // Applies every IEntityTypeConfiguration<T> in this assembly - one file per entity,
        // Fluent API only, no data annotations. AppUserConfiguration renames the Identity user
        // table from "AspNetUsers" to "AppUsers" and configures FoundU's custom columns; the
        // remaining user-related Identity tables are renamed below for consistency.
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("AppUserClaims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("AppUserLogins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("AppUserTokens");
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
    /// Stamps CreatedAt/UpdatedAt (UTC) on every tracked BaseEntity, converts hard deletes into
    /// soft deletes for ISoftDeletable entities, and does the same for AppUser - which can't
    /// extend BaseEntity itself because it already inherits IdentityUser&lt;Guid&gt;.
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
                    entry.State = EntityState.Modified;
                    softDeletable.IsDeleted = true;
                    softDeletable.DeletedAt = utcNow;
                    entry.Entity.UpdatedAt = utcNow;
                    break;
            }
        }

        foreach (var entry in ChangeTracker.Entries<AppUser>())
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
                case EntityState.Deleted:
                    // AppUser is ISoftDeletable - convert hard deletes into soft deletes here too.
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = utcNow;
                    entry.Entity.UpdatedAt = utcNow;
                    break;
            }
        }
    }
}
