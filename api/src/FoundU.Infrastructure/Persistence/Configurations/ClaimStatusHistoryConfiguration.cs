using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class ClaimStatusHistoryConfiguration : IEntityTypeConfiguration<ClaimStatusHistory>
{
    public void Configure(EntityTypeBuilder<ClaimStatusHistory> builder)
    {
        builder.ToTable("ClaimStatusHistories");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.FromStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(h => h.Reason).HasMaxLength(500);

        builder.Property(h => h.ChangedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(h => h.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(h => h.UpdatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasOne(h => h.Claim)
            .WithMany(c => c.StatusHistory)
            .HasForeignKey(h => h.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.ChangedByUser)
            .WithMany()
            .HasForeignKey(h => h.ChangedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(h => h.ClaimId);
        builder.HasIndex(h => h.ChangedAt);
    }
}
