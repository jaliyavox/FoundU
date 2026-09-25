using FoundU.Domain.Entities;
using FoundU.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class StorageLocationConfiguration : IEntityTypeConfiguration<StorageLocation>
{
    private static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<StorageLocation> builder)
    {
        builder.ToTable("StorageLocations");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(150).IsRequired();
        builder.Property(s => s.Building).HasMaxLength(150);

        builder.Property(s => s.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(s => s.DeletedAt).HasColumnType("timestamptz");

        builder.HasIndex(s => s.Name).IsUnique();

        builder.HasQueryFilter(s => !s.IsDeleted);

        builder.HasData(
            new StorageLocation
            {
                Id = SeedIds.StorageSecurityDeskA,
                Name = "Security Desk - Building A",
                Building = "Building A",
                Capacity = 200,
                IsActive = true,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp
            },
            new StorageLocation { Id = SeedIds.StorageLibraryDesk, Name = "Library Front Desk", Building = "Building C", Capacity = 80, IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new StorageLocation { Id = SeedIds.StorageSportsComplexOffice, Name = "Sports Complex Office", Building = "Sports Block", Capacity = 60, IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new StorageLocation { Id = SeedIds.StorageStudentServices, Name = "Student Services", Building = "Building A", Capacity = 120, IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp }
        );
    }
}
