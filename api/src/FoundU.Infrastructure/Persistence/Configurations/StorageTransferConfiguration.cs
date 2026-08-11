using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class StorageTransferConfiguration : IEntityTypeConfiguration<StorageTransfer>
{
    public void Configure(EntityTypeBuilder<StorageTransfer> builder)
    {
        builder.ToTable("StorageTransfers");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Reason).HasMaxLength(500);
        builder.Property(t => t.TransferredAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(t => t.UpdatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasOne(t => t.FoundReport)
            .WithMany(r => r.StorageTransfers)
            .HasForeignKey(t => t.FoundReportId)
            .OnDelete(DeleteBehavior.Cascade); // transfer history belongs to the found report

        // Two FKs into StorageLocation - both Restrict to avoid multiple cascade paths in Postgres.
        builder.HasOne(t => t.FromStorageLocation)
            .WithMany(s => s.TransfersFrom)
            .HasForeignKey(t => t.FromStorageLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.ToStorageLocation)
            .WithMany(s => s.TransfersTo)
            .HasForeignKey(t => t.ToStorageLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.TransferredByUser)
            .WithMany(u => u.StorageTransfers)
            .HasForeignKey(t => t.TransferredByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.FoundReportId);
        builder.HasIndex(t => t.TransferredAt);
    }
}
