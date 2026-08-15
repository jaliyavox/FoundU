using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class VerificationQuestionConfiguration : IEntityTypeConfiguration<VerificationQuestion>
{
    public void Configure(EntityTypeBuilder<VerificationQuestion> builder)
    {
        builder.ToTable("VerificationQuestions");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.QuestionText).HasMaxLength(500).IsRequired();
        builder.Property(q => q.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(q => q.UpdatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasOne(q => q.Claim)
            .WithMany(c => c.VerificationQuestions)
            .HasForeignKey(q => q.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(q => q.GeneratedByAgentRun)
            .WithMany(a => a.VerificationQuestions)
            .HasForeignKey(q => q.GeneratedByAgentRunId)
            .OnDelete(DeleteBehavior.SetNull);

        // 1:1 with ClaimAnswer, principal = VerificationQuestion
        builder.HasOne(q => q.Answer)
            .WithOne(a => a.VerificationQuestion)
            .HasForeignKey<ClaimAnswer>(a => a.VerificationQuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(q => q.ClaimId);
    }
}
