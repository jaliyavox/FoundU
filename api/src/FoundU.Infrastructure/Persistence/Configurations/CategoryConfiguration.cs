using FoundU.Domain.Entities;
using FoundU.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    private static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(500);

        builder.Property(c => c.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(c => c.DeletedAt).HasColumnType("timestamptz");

        builder.HasIndex(c => c.Name).IsUnique();

        builder.HasQueryFilter(c => !c.IsDeleted);

        builder.HasData(
            new Category { Id = SeedIds.CategoryElectronics, Name = "Electronics", Description = "Phones, laptops, chargers, headphones", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new Category { Id = SeedIds.CategoryBagsWallets, Name = "Bags & Wallets", Description = "Backpacks, handbags, wallets, purses", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new Category { Id = SeedIds.CategoryClothing, Name = "Clothing", Description = "Jackets, hats, scarves and other apparel", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new Category { Id = SeedIds.CategoryDocumentsCards, Name = "Documents & Cards", Description = "Student IDs, cards, documents", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new Category { Id = SeedIds.CategoryKeys, Name = "Keys", Description = "House, locker, or vehicle keys", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new Category { Id = SeedIds.CategoryJewelryAccessories, Name = "Jewelry & Accessories", Description = "Watches, jewelry, glasses", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new Category { Id = SeedIds.CategoryBooksStationery, Name = "Books & Stationery", Description = "Textbooks, notebooks, stationery", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new Category { Id = SeedIds.CategoryIdentityDocuments, Name = "ID & Licences", Description = "University ID, national ID, driving licence, passport", IsActive = true, IsHighlighted = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new Category { Id = SeedIds.CategoryOther, Name = "Other", Description = "Anything not covered above", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp }
        );
    }
}
