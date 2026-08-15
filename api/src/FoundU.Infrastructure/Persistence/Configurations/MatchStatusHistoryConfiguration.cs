using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class MatchStatusHistoryConfiguration : IEntityTypeConfiguration<MatchStatusHistory>
{
    public void Configure(EntityTypeBuilder<MatchStatusHistory> builder)
    {
        builder.ToTable("MatchStatusHistories");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.FromStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(h => h.Reason).HasMaxLength(500);

        builder.Property(h => h.ChangedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(h => h.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(h => h.UpdatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasOne(h => h.MatchSuggestion)
            .WithMany(m => m.StatusHistory)
            .HasForeignKey(h => h.MatchSuggestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.ChangedByUser)
            .WithMany()
            .HasForeignKey(h => h.ChangedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(h => h.MatchSuggestionId);
        builder.HasIndex(h => h.ChangedAt);
    }
}
