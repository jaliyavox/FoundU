using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class FoundItemPhotoConfiguration : IEntityTypeConfiguration<FoundItemPhoto>
{
    public void Configure(EntityTypeBuilder<FoundItemPhoto> builder)
    {
        builder.ToTable("FoundItemPhotos");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Url).HasMaxLength(1000).IsRequired();
        builder.Property(p => p.UploadedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(p => p.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(p => p.DeletedAt).HasColumnType("timestamptz");

        builder.HasOne(p => p.FoundReport)
            .WithMany(r => r.Photos)
            .HasForeignKey(p => p.FoundReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.FoundReportId);

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
