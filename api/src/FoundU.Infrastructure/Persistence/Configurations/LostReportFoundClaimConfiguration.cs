using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class LostReportFoundClaimConfiguration : IEntityTypeConfiguration<LostReportFoundClaim>
{
    public void Configure(EntityTypeBuilder<LostReportFoundClaim> builder)
    {
        builder.ToTable("LostReportFoundClaims");
        builder.HasKey(c => c.Id);
        builder.HasRowVersion();

        builder.Property(c => c.SeenAt).HasColumnType("timestamptz");
        builder.Property(c => c.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasOne(c => c.LostReport)
            .WithMany(r => r.FoundClaims)
            .HasForeignKey(c => c.LostReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Finder)
            .WithMany()
            .HasForeignKey(c => c.FinderId)
            .OnDelete(DeleteBehavior.Restrict);

        // One per person per report: pressing the button twice is the same claim, not two
        // people finding the same item. The unique index makes that a database rule rather
        // than a hope, so a double-click cannot inflate the count the author sees.
        builder.HasIndex(c => new { c.LostReportId, c.FinderId }).IsUnique();
        builder.HasIndex(c => new { c.LostReportId, c.CreatedAt });

        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(c => c.HandoverCode).HasMaxLength(6).IsFixedLength();
        builder.Property(c => c.CollectionCheck).HasMaxLength(200);
        builder.Property(c => c.HandoverStartedAt).HasColumnType("timestamptz");
        builder.Property(c => c.HandoverExpiresAt).HasColumnType("timestamptz");
        builder.Property(c => c.HandedInAt).HasColumnType("timestamptz");
        builder.Property(c => c.CollectedAt).HasColumnType("timestamptz");

        // A live code has to mean exactly one handover, or the desk cannot tell two apart.
        // Filtered, because a code is cleared the moment the item is collected.
        builder.HasIndex(c => c.HandoverCode)
            .IsUnique()
            .HasDatabaseName("IX_LostReportFoundClaims_HandoverCode_Unique")
            .HasFilter("\"HandoverCode\" IS NOT NULL");

        builder.HasOne(c => c.ReceivedByStaff)
            .WithMany()
            .HasForeignKey(c => c.ReceivedByStaffId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.CollectedByStaff)
            .WithMany()
            .HasForeignKey(c => c.CollectedByStaffId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.FoundReport)
            .WithMany()
            .HasForeignKey(c => c.FoundReportId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
