using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class AgentStepConfiguration : IEntityTypeConfiguration<AgentStep>
{
    public void Configure(EntityTypeBuilder<AgentStep> builder)
    {
        builder.ToTable("AgentSteps");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.AgentName)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(s => s.Task).HasMaxLength(300).IsRequired();
        builder.Property(s => s.InputJson).HasColumnType("jsonb");
        builder.Property(s => s.OutputJson).HasColumnType("jsonb");
        builder.Property(s => s.ErrorMessage).HasMaxLength(1000);

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.StartedAt).HasColumnType("timestamptz");
        builder.Property(s => s.CompletedAt).HasColumnType("timestamptz");
        builder.Property(s => s.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasOne(s => s.AgentRun)
            .WithMany(a => a.Steps)
            .HasForeignKey(s => s.AgentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        // A run should not record the same step order twice
        builder.HasIndex(s => new { s.AgentRunId, s.StepOrder }).IsUnique();
    }
}
