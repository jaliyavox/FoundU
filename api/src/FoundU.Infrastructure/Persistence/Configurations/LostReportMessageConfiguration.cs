using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class LostReportMessageConfiguration : IEntityTypeConfiguration<LostReportMessage>
{
    public void Configure(EntityTypeBuilder<LostReportMessage> builder)
    {
        builder.ToTable("LostReportMessages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Body).HasMaxLength(1000).IsRequired();
        builder.Property(m => m.ReadAt).HasColumnType("timestamptz");
        builder.Property(m => m.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(m => m.UpdatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasOne(m => m.LostReport)
            .WithMany()
            .HasForeignKey(m => m.LostReportId)
            .OnDelete(DeleteBehavior.Cascade);

        // Keep the sender row even if their account is removed - deleting a person should not
        // silently rewrite a conversation the report's author has already read.
        builder.HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        // The owner's inbox reads newest-first per report.
        builder.HasIndex(m => new { m.LostReportId, m.CreatedAt });
        builder.HasIndex(m => m.SenderId);
    }
}
