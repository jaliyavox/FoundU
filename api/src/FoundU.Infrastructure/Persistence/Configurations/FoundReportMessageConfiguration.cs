using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class FoundReportMessageConfiguration : IEntityTypeConfiguration<FoundReportMessage>
{
    public void Configure(EntityTypeBuilder<FoundReportMessage> builder)
    {
        builder.ToTable("FoundReportMessages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Body).HasMaxLength(2000).IsRequired();
        builder.Property(m => m.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(m => m.UpdatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasOne(m => m.FoundReport)
            .WithMany()
            .HasForeignKey(m => m.FoundReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Recipient)
            .WithMany()
            .HasForeignKey(m => m.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);

        // Reading a thread is "this item, these two people, in order".
        builder.HasIndex(m => new { m.FoundReportId, m.CreatedAt });
        builder.HasIndex(m => new { m.FoundReportId, m.SenderId });
    }
}
