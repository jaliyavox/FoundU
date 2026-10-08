using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class StoredPhotoConfiguration : IEntityTypeConfiguration<StoredPhoto>
{
    public void Configure(EntityTypeBuilder<StoredPhoto> builder)
    {
        builder.ToTable("StoredPhotos");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.ContentType).HasMaxLength(40).IsRequired();
        builder.Property(p => p.Data).IsRequired();
    }
}
