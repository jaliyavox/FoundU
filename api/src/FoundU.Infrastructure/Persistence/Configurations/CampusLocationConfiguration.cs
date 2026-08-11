using FoundU.Domain.Entities;
using FoundU.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class CampusLocationConfiguration : IEntityTypeConfiguration<CampusLocation>
{
    private static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<CampusLocation> builder)
    {
        builder.ToTable("CampusLocations");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Name).HasMaxLength(150).IsRequired();
        builder.Property(l => l.Building).HasMaxLength(150);
        builder.Property(l => l.Description).HasMaxLength(500);

        builder.Property(l => l.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(l => l.UpdatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(l => l.DeletedAt).HasColumnType("timestamptz");

        builder.HasIndex(l => new { l.Name, l.Building }).IsUnique();

        builder.HasQueryFilter(l => !l.IsDeleted);

        builder.HasData(
            new CampusLocation { Id = SeedIds.LocationLibrary, Name = "Library", Building = "Building C", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new CampusLocation { Id = SeedIds.LocationLectureHallB12, Name = "Lecture Hall B12", Building = "Building B", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new CampusLocation { Id = SeedIds.LocationCafeteria, Name = "Cafeteria", Building = "Building A", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new CampusLocation { Id = SeedIds.LocationSportsComplex, Name = "Sports Complex", Building = "Sports Block", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new CampusLocation { Id = SeedIds.LocationMainAuditorium, Name = "Main Auditorium", Building = "Building A", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new CampusLocation { Id = SeedIds.LocationSecurityDeskBuildingA, Name = "Security Desk", Building = "Building A", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new CampusLocation { Id = SeedIds.LocationParkingLot, Name = "Parking Lot", Building = "Outdoor", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp }
        );
    }
}
