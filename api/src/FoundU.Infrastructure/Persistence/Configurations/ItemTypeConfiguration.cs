using FoundU.Domain.Entities;
using FoundU.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class ItemTypeConfiguration : IEntityTypeConfiguration<ItemType>
{
    private static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<ItemType> builder)
    {
        builder.ToTable("ItemTypes");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();

        builder.Property(t => t.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(t => t.UpdatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(t => t.DeletedAt).HasColumnType("timestamptz");

        builder.HasOne(t => t.Category)
            .WithMany(c => c.ItemTypes)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // An ItemType name only needs to be unique within its own Category
        // (e.g. "Charger" could exist under both Electronics and Other).
        builder.HasIndex(t => new { t.CategoryId, t.Name }).IsUnique();

        builder.HasQueryFilter(t => !t.IsDeleted);

        builder.HasData(
            new ItemType { Id = SeedIds.ItemTypeLaptop, CategoryId = SeedIds.CategoryElectronics, Name = "Laptop", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypePhone, CategoryId = SeedIds.CategoryElectronics, Name = "Phone", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeHeadphones, CategoryId = SeedIds.CategoryElectronics, Name = "Headphones", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeEarphones, CategoryId = SeedIds.CategoryElectronics, Name = "Earphones", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeBackpack, CategoryId = SeedIds.CategoryBagsWallets, Name = "Backpack", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeLaptopBag, CategoryId = SeedIds.CategoryBagsWallets, Name = "Laptop Bag", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypePurse, CategoryId = SeedIds.CategoryBagsWallets, Name = "Purse", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeWallet, CategoryId = SeedIds.CategoryBagsWallets, Name = "Wallet", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp }
        );
    }
}
