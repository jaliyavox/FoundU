using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        // Renames Identity's default "AspNetUsers" table. Identity's own base.OnModelCreating()
        // already configured Id/UserName/NormalizedUserName/Email/NormalizedEmail/
        // EmailConfirmed/PasswordHash/SecurityStamp/ConcurrencyStamp/PhoneNumber/
        // PhoneNumberConfirmed/TwoFactorEnabled/LockoutEnd/LockoutEnabled/AccessFailedCount and
        // the unique "UserNameIndex" on NormalizedUserName - only FoundU's custom columns are
        // configured here.
        builder.ToTable("AppUsers");

        builder.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.StudentNumber).HasMaxLength(50);
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

        // StudentNumber identifies one student when present, but is optional (Staff/Admin
        // accounts don't have one) - a plain unique index would reject a second NULL, so this
        // is a partial/filtered unique index that only applies to non-null values.
        builder.HasIndex(u => u.StudentNumber)
            .IsUnique()
            .HasDatabaseName("IX_AppUsers_StudentNumber_Unique")
            .HasFilter("\"StudentNumber\" IS NOT NULL");

        builder.HasIndex(u => u.Role);
        builder.HasIndex(u => u.IsSuspended);

        // Self-referencing FK: who suspended this user
        builder.HasOne(u => u.SuspendedByUser)
            .WithMany()
            .HasForeignKey(u => u.SuspendedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Soft delete global filter
        builder.HasQueryFilter(u => !u.IsDeleted);

        // NOTE: the Admin account is intentionally NOT seeded here via HasData. HasData runs
        // unconditionally in every environment (including production). See
        // Seed/DevelopmentDataSeeder.cs - it only runs when IsDevelopment() is true, creates the
        // account via UserManager.CreateAsync (so the password goes through Identity's own
        // PasswordHasher, never a hand-rolled hash), and reads the password from configuration.
    }
}
