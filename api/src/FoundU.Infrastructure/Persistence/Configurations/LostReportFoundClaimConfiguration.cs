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

        builder.Property(c => c.SeenAt).HasColumnType("timestamptz");
        builder.Property(c => c.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasOne(c => c.LostReport)
            .WithMany()
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
    }
}
