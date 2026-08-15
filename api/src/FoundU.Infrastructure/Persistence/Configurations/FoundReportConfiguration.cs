using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class FoundReportConfiguration : IEntityTypeConfiguration<FoundReport>
{
    public void Configure(EntityTypeBuilder<FoundReport> builder)
    {
        builder.ToTable("FoundReports");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.GeneralDescription).HasMaxLength(1000).IsRequired();

        // Nullable: some items have no useful unique hidden detail. The service layer decides
        // whether what's here is sufficient for automated verification vs. routing to manual review.
        builder.Property(r => r.PrivateVerificationDetails).HasMaxLength(1000);

        builder.Property(r => r.ObservedAttributesJson).HasColumnType("jsonb");
        builder.Property(r => r.PrivateVerificationAttributesJson).HasColumnType("jsonb");

        builder.Property(r => r.PrimaryColor).HasMaxLength(50);
        builder.Property(r => r.SecondaryColor).HasMaxLength(50);

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.FoundAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(r => r.DeletedAt).HasColumnType("timestamptz");

        builder.HasOne(r => r.Staff)
            .WithMany(u => u.FoundReports)
            .HasForeignKey(r => r.StaffId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Category)
            .WithMany(c => c.FoundReports)
            .HasForeignKey(r => r.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ItemType)
            .WithMany(t => t.FoundReports)
            .HasForeignKey(r => r.ItemTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.FoundLocation)
            .WithMany(l => l.FoundReportsFoundHere)
            .HasForeignKey(r => r.FoundLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.StorageLocation)
            .WithMany(s => s.FoundReports)
            .HasForeignKey(r => r.StorageLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.StaffId);
        builder.HasIndex(r => r.CategoryId);
        builder.HasIndex(r => r.ItemTypeId);
        builder.HasIndex(r => r.FoundLocationId);
        builder.HasIndex(r => r.StorageLocationId);
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.FoundAt);
        builder.HasIndex(r => new { r.Status, r.CategoryId, r.ItemTypeId, r.FoundLocationId });

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
