using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    private static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("AppUsers");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.StudentNumber).HasMaxLength(50);
        builder.Property(u => u.PhoneNumber).HasMaxLength(30);
        builder.Property(u => u.SuspensionReason).HasMaxLength(500);

        // Enum stored as string
        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(u => u.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(u => u.UpdatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(u => u.SuspendedAt).HasColumnType("timestamptz");
        builder.Property(u => u.DeletedAt).HasColumnType("timestamptz");

        // Unique email (case-insensitive citext could be used; simple unique index here)
        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.Role);
        builder.HasIndex(u => u.IsSuspended);

        // Self-referencing FK: who suspended this user
        builder.HasOne(u => u.SuspendedByUser)
            .WithMany()
            .HasForeignKey(u => u.SuspendedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Soft delete global filter
        builder.HasQueryFilter(u => !u.IsDeleted);

        // Seed a single Admin account. PasswordHash below is a placeholder BCrypt hash for
        // the value "ChangeMe123!" - MUST be rotated immediately after first deployment.
        builder.HasData(
            new AppUser
            {
                Id = SeedIds.AdminUserId,
                FullName = "FoundU Administrator",
                Email = "admin@foundu.university.edu",
                PasswordHash = "$2a$11$K9x3yQFqZ8h5oQxWc0m9UuG7l1i6f2Hs0z3s0R9Zt0v2E9c1lYyDe", // placeholder - rotate on first login
                Role = UserRole.Admin,
                IsSuspended = false,
                IsDeleted = false,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp
            }
        );
    }
}
