using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace FoundU.Infrastructure.Persistence.Configurations;
public class FoundVerificationEvidenceConfiguration : IEntityTypeConfiguration<FoundVerificationEvidence>
{
    public void Configure(EntityTypeBuilder<FoundVerificationEvidence> builder)
    {
        builder.ToTable("FoundVerificationEvidence");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Detail).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnType("timestamptz");
        builder.Property(e => e.UpdatedAt).HasColumnType("timestamptz");
        builder.HasOne(e => e.FoundReport).WithMany().HasForeignKey(e => e.FoundReportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.RecordedByUser).WithMany().HasForeignKey(e => e.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
