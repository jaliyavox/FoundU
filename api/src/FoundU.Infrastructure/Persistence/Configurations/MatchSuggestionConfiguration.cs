using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class MatchSuggestionConfiguration : IEntityTypeConfiguration<MatchSuggestion>
{
    public void Configure(EntityTypeBuilder<MatchSuggestion> builder)
    {
        builder.ToTable("MatchSuggestions");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.MatchScore).HasColumnType("numeric(4,3)").IsRequired(); // 0.000 - 1.000
        builder.Property(m => m.MatchingFactorsJson).HasColumnType("jsonb");
        builder.Property(m => m.ConflictingFactorsJson).HasColumnType("jsonb");

        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(m => m.UpdatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasOne(m => m.LostReport)
            .WithMany(r => r.MatchSuggestions)
            .HasForeignKey(m => m.LostReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.FoundReport)
            .WithMany(r => r.MatchSuggestions)
            .HasForeignKey(m => m.FoundReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.GeneratedByAgentRun)
            .WithMany(a => a.MatchSuggestions)
            .HasForeignKey(m => m.GeneratedByAgentRunId)
            .OnDelete(DeleteBehavior.SetNull);

        // A given lost/found pair should only ever have one active suggestion record
        builder.HasIndex(m => new { m.LostReportId, m.FoundReportId }).IsUnique();
        builder.HasIndex(m => m.Status);
        builder.HasIndex(m => m.MatchScore);
    }
}
