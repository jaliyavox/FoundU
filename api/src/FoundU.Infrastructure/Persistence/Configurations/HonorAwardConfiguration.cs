using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class HonorAwardConfiguration : IEntityTypeConfiguration<HonorAward>
{
    public void Configure(EntityTypeBuilder<HonorAward> builder)
    {
        builder.ToTable("HonorAwards");
        builder.HasKey(a => a.Id);

        // Stored as text: a number in the database would silently re-map every award if
        // someone reorders the enum, and these are meant to be readable in a query.
        builder.Property(a => a.Reason).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(a => a.Detail).HasMaxLength(200).IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(a => a.UpdatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.LostReport)
            .WithMany()
            .HasForeignKey(a => a.LostReportId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.FoundReport)
            .WithMany()
            .HasForeignKey(a => a.FoundReportId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(a => new { a.UserId, a.CreatedAt });

        // The same outcome must not pay twice. Postgres treats NULLs in a unique index as
        // distinct, so each pairing gets its own filtered index rather than one index over
        // both nullable columns.
        builder.HasIndex(a => new { a.UserId, a.Reason, a.LostReportId })
            .IsUnique()
            .HasFilter("\"LostReportId\" IS NOT NULL");
        builder.HasIndex(a => new { a.UserId, a.Reason, a.FoundReportId })
            .IsUnique()
            .HasFilter("\"FoundReportId\" IS NOT NULL");
    }
}
