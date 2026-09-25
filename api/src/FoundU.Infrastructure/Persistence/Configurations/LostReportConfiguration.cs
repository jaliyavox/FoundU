using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class LostReportConfiguration : IEntityTypeConfiguration<LostReport>
{
    public void Configure(EntityTypeBuilder<LostReport> builder)
    {
        builder.ToTable("LostReports", t =>
            t.HasCheckConstraint(
                "CK_LostReports_EstimatedLostRange",
                "\"EstimatedLostFromAt\" <= \"EstimatedLostToAt\""));

        builder.HasKey(r => r.Id);

        // Fixed width, and unique: the desk looks reports up by this alone.
        builder.Property(r => r.HandInCode).HasMaxLength(6).IsFixedLength().IsRequired();
        builder.HasIndex(r => r.HandInCode).IsUnique();

        // A flag should survive the flagger's account being removed.
        builder.HasOne(r => r.FlaggedByUser)
            .WithMany()
            .HasForeignKey(r => r.FlaggedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // The moderation queue is "everything flagged, oldest first".
        builder.HasIndex(r => new { r.IsFlagged, r.FlaggedAt });

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

        builder.Property(r => r.EstimatedLostFromAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(r => r.EstimatedLostToAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(r => r.WithdrawnAt).HasColumnType("timestamptz");
        builder.Property(r => r.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(r => r.DeletedAt).HasColumnType("timestamptz");

        // Relationships
        builder.HasOne(r => r.Student)
            .WithMany(u => u.LostReports)
            .HasForeignKey(r => r.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Category)
            .WithMany(c => c.LostReports)
            .HasForeignKey(r => r.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ItemType)
            .WithMany(t => t.LostReports)
            .HasForeignKey(r => r.ItemTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.LastSeenLocation)
            .WithMany(l => l.LostReportsLastSeenHere)
            .HasForeignKey(r => r.LastSeenLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes for search/filter/sort
        builder.HasIndex(r => r.StudentId);
        builder.HasIndex(r => r.CategoryId);
        builder.HasIndex(r => r.ItemTypeId);
        builder.HasIndex(r => r.LastSeenLocationId);
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => new { r.EstimatedLostFromAt, r.EstimatedLostToAt });
        builder.HasIndex(r => new { r.Status, r.CategoryId, r.ItemTypeId, r.LastSeenLocationId });

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
