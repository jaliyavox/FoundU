using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class LostItemPhotoConfiguration : IEntityTypeConfiguration<LostItemPhoto>
{
    public void Configure(EntityTypeBuilder<LostItemPhoto> builder)
    {
        builder.ToTable("LostItemPhotos");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Url).HasMaxLength(1000).IsRequired();
        builder.Property(p => p.UploadedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(p => p.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(p => p.DeletedAt).HasColumnType("timestamptz");

        builder.HasOne(p => p.LostReport)
            .WithMany(r => r.Photos)
            .HasForeignKey(p => p.LostReportId)
            .OnDelete(DeleteBehavior.Cascade); // photos are owned by the report

        builder.HasIndex(p => p.LostReportId);

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
