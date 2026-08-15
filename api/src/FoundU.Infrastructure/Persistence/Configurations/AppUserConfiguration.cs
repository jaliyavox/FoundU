using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("AppUsers");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.NormalizedEmail).HasMaxLength(256).IsRequired();
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

        // Case-insensitive uniqueness: enforced against NormalizedEmail (kept in sync by
        // FoundUDbContext.SaveChanges), not the raw Email column, so "a@x.com" and "A@x.com"
        // can never both register.
        builder.HasIndex(u => u.NormalizedEmail).IsUnique();

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

        // NOTE: the Admin account is intentionally NOT seeded here via HasData anymore.
        // HasData runs unconditionally in every environment (including production), which is
        // exactly the "looks like a production credential" problem flagged in review.
        // See Seed/DevelopmentDataSeeder.cs - it only runs when
        // IWebHostEnvironment.IsDevelopment() is true, and reads its admin password from
        // configuration/environment variables instead of a hardcoded hash.
    }
}
