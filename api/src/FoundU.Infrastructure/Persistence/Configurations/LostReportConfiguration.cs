using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class LostReportConfiguration : IEntityTypeConfiguration<LostReport>
{
    public void Configure(EntityTypeBuilder<LostReport> builder)
    {
        builder.ToTable("LostReports");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Description).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.PrimaryColor).HasMaxLength(50);
        builder.Property(r => r.SecondaryColor).HasMaxLength(50);
        builder.Property(r => r.IdentifyingFeaturesJson).HasColumnType("jsonb");
        builder.Property(r => r.ParsedAttributesJson).HasColumnType("jsonb");
        builder.Property(r => r.WithdrawReason).HasMaxLength(500);

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.LastSeenAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(r => r.WithdrawnAt).HasColumnType("timestamptz");
        builder.Property(r => r.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(r => r.DeletedAt).HasColumnType("timestamptz");

        // Relationships
        builder.HasOne(r => r.Student)
            .WithMany(u => u.LostReports)
            .HasForeignKey(r => r.StudentId)
            .OnDelete(DeleteBehavior.Restrict); // never cascade-delete a user's history

        builder.HasOne(r => r.Category)
            .WithMany(c => c.LostReports)
            .HasForeignKey(r => r.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.LastSeenLocation)
            .WithMany(l => l.LostReportsLastSeenHere)
            .HasForeignKey(r => r.LastSeenLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes for search/filter/sort (Section 5/12 requirements)
        builder.HasIndex(r => r.StudentId);
        builder.HasIndex(r => r.CategoryId);
        builder.HasIndex(r => r.LastSeenLocationId);
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.LastSeenAt);
        builder.HasIndex(r => new { r.Status, r.CategoryId, r.LastSeenLocationId });

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
