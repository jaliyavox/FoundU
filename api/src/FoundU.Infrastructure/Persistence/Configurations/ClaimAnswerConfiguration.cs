using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class ClaimAnswerConfiguration : IEntityTypeConfiguration<ClaimAnswer>
{
    public void Configure(EntityTypeBuilder<ClaimAnswer> builder)
    {
        builder.ToTable("ClaimAnswers");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.AnswerText).HasMaxLength(500).IsRequired();
        builder.Property(a => a.SubmittedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(a => a.UpdatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasOne(a => a.Claim)
            .WithMany(c => c.Answers)
            .HasForeignKey(a => a.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);

        // One answer per question
        builder.HasIndex(a => a.VerificationQuestionId).IsUnique();
        builder.HasIndex(a => a.ClaimId);
    }
}
