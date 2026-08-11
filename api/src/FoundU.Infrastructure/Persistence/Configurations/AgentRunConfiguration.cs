using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class AgentRunConfiguration : IEntityTypeConfiguration<AgentRun>
{
    public void Configure(EntityTypeBuilder<AgentRun> builder)
    {
        builder.ToTable("AgentRuns");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.TriggerEntityType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Objective).HasMaxLength(500).IsRequired();
        builder.Property(a => a.PlanJson).HasColumnType("jsonb");
        builder.Property(a => a.ErrorMessage).HasMaxLength(1000);
        builder.Property(a => a.FinalOutcomeJson).HasColumnType("jsonb");

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(a => a.StartedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(a => a.CompletedAt).HasColumnType("timestamptz");
        builder.Property(a => a.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(a => a.UpdatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasOne(a => a.Claim)
            .WithMany(c => c.AgentRuns)
            .HasForeignKey(a => a.ClaimId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(a => a.ClaimId);
        builder.HasIndex(a => new { a.TriggerEntityType, a.TriggerEntityId });
        builder.HasIndex(a => a.Status);
    }
}
