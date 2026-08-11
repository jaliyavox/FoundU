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

        // NOTE: the 1:1 Claim <-> ApprovalDecision relationship (and its unique FK constraint on
        // ClaimId) is configured from the principal side in ClaimConfiguration - not repeated here
        // to avoid a duplicate index definition.

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
