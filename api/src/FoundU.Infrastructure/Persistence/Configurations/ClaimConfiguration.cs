using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class ClaimConfiguration : IEntityTypeConfiguration<Claim>
{
    public void Configure(EntityTypeBuilder<Claim> builder)
    {
        builder.ToTable("Claims");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(c => c.DeletedAt).HasColumnType("timestamptz");

        builder.HasOne(c => c.Student)
            .WithMany(u => u.Claims)
            .HasForeignKey(c => c.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.LostReport)
            .WithMany(r => r.Claims)
            .HasForeignKey(c => c.LostReportId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.FoundReport)
            .WithMany(r => r.Claims)
            .HasForeignKey(c => c.FoundReportId)
            .OnDelete(DeleteBehavior.Restrict);

        // 1:N - a claim can be revised multiple times (RevisionRequested -> Approved, etc).
        // Full navigation + FK configured from the ApprovalDecision side (see
        // ApprovalDecisionConfiguration); nothing to configure here for that relationship.

        builder.HasIndex(c => c.StudentId);
        builder.HasIndex(c => c.LostReportId);
        builder.HasIndex(c => c.FoundReportId);
        builder.HasIndex(c => c.Status);

        // BUSINESS RULE: "Only one approved Claim may exist for a FoundReport."
        // Postgres partial (filtered) unique index - only enforced among rows where Status = 'Approved'.
        builder.HasIndex(c => c.FoundReportId)
            .IsUnique()
            .HasDatabaseName("IX_Claims_FoundReportId_OneApproved")
            .HasFilter("\"Status\" = 'Approved'");

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
