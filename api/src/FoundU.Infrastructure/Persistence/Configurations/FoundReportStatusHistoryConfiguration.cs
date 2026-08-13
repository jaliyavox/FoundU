using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class FoundReportStatusHistoryConfiguration : IEntityTypeConfiguration<FoundReportStatusHistory>
{
    public void Configure(EntityTypeBuilder<FoundReportStatusHistory> builder)
    {
        builder.ToTable("FoundReportStatusHistories");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.FromStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(h => h.Reason).HasMaxLength(500);

        builder.Property(h => h.ChangedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(h => h.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(h => h.UpdatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasOne(h => h.FoundReport)
            .WithMany(r => r.StatusHistory)
            .HasForeignKey(h => h.FoundReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.ChangedByUser)
            .WithMany()
            .HasForeignKey(h => h.ChangedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(h => h.FoundReportId);
        builder.HasIndex(h => h.ChangedAt);
    }
}
