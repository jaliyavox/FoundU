using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(t => t.Id);

        // Only the SHA-256 hash is stored - never the raw token - so a DB leak alone can't be
        // replayed. See RefreshToken.cs summary.
        builder.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(t => t.CreatedByIp).HasMaxLength(64);
        builder.Property(t => t.RevokedByIp).HasMaxLength(64);
        builder.Property(t => t.ReplacedByTokenHash).HasMaxLength(128);
        builder.Property(t => t.ReasonRevoked).HasMaxLength(200);

        builder.Property(t => t.ExpiresAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(t => t.RevokedAt).HasColumnType("timestamptz");
        builder.Property(t => t.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(t => t.UpdatedAt).HasColumnType("timestamptz").IsRequired();

        builder.Ignore(t => t.IsExpired);
        builder.Ignore(t => t.IsRevoked);
        builder.Ignore(t => t.IsActive);

        builder.HasOne(t => t.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => new { t.UserId, t.ExpiresAt });
    }
}
