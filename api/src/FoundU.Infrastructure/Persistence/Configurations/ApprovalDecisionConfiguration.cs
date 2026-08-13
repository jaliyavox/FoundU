using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class ApprovalDecisionConfiguration : IEntityTypeConfiguration<ApprovalDecision>
{
    public void Configure(EntityTypeBuilder<ApprovalDecision> builder)
    {
        builder.ToTable("ApprovalDecisions");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Decision)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(a => a.Reason).HasMaxLength(1000);
        builder.Property(a => a.DecidedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(a => a.UpdatedAt).HasColumnType("timestamptz").IsRequired();

        // 1:N - Claim -> ApprovalDecisions. A claim keeps a full decision history (e.g.
        // RevisionRequested, then later Approved) instead of a single final decision, so this
        // FK is a plain (non-unique) index, not a one-to-one constraint.
        builder.HasOne(a => a.Claim)
            .WithMany(c => c.ApprovalDecisions)
            .HasForeignKey(a => a.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.ClaimId);

        builder.HasOne(a => a.DecidedByUser)
            .WithMany(u => u.ApprovalDecisions)
            .HasForeignKey(a => a.DecidedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.OverriddenByUser)
            .WithMany()
            .HasForeignKey(a => a.OverriddenByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.IsOverride);
    }
}
