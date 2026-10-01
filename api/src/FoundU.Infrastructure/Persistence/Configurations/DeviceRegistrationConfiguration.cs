using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class DeviceRegistrationConfiguration : IEntityTypeConfiguration<DeviceRegistration>
{
    public void Configure(EntityTypeBuilder<DeviceRegistration> builder)
    {
        builder.ToTable("DeviceRegistrations");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.FcmToken).HasMaxLength(4096).IsRequired();
        builder.Property(d => d.Platform).HasMaxLength(20).IsRequired();
        builder.Property(d => d.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(d => d.UpdatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(d => d.DeactivatedAt).HasColumnType("timestamptz");

        builder.HasOne(d => d.User)
            .WithMany(u => u.DeviceRegistrations)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // A physical FCM token maps to a single current account. Re-registration safely moves it
        // after an account switch instead of exposing or duplicating the token.
        builder.HasIndex(d => d.FcmToken).IsUnique();
        builder.HasIndex(d => new { d.UserId, d.IsActive });
    }
}
