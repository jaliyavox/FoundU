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
            new ItemType { Id = SeedIds.ItemTypeWallet, CategoryId = SeedIds.CategoryBagsWallets, Name = "Wallet", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeJacket, CategoryId = SeedIds.CategoryClothing, Name = "Jacket", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeHoodie, CategoryId = SeedIds.CategoryClothing, Name = "Hoodie", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeScarf, CategoryId = SeedIds.CategoryClothing, Name = "Scarf", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeCap, CategoryId = SeedIds.CategoryClothing, Name = "Cap", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeUmbrella, CategoryId = SeedIds.CategoryClothing, Name = "Umbrella", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeStudentCard, CategoryId = SeedIds.CategoryDocumentsCards, Name = "Student Card", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeIdCard, CategoryId = SeedIds.CategoryDocumentsCards, Name = "ID Card", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeBusPass, CategoryId = SeedIds.CategoryDocumentsCards, Name = "Bus Pass", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeBankCard, CategoryId = SeedIds.CategoryDocumentsCards, Name = "Bank Card", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeHouseKeys, CategoryId = SeedIds.CategoryKeys, Name = "House Keys", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeCarKeys, CategoryId = SeedIds.CategoryKeys, Name = "Car Keys", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeLockerKey, CategoryId = SeedIds.CategoryKeys, Name = "Locker Key", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeWatch, CategoryId = SeedIds.CategoryJewelryAccessories, Name = "Watch", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeGlasses, CategoryId = SeedIds.CategoryJewelryAccessories, Name = "Glasses", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeRing, CategoryId = SeedIds.CategoryJewelryAccessories, Name = "Ring", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeBracelet, CategoryId = SeedIds.CategoryJewelryAccessories, Name = "Bracelet", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeTextbook, CategoryId = SeedIds.CategoryBooksStationery, Name = "Textbook", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeNotebook, CategoryId = SeedIds.CategoryBooksStationery, Name = "Notebook", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeCalculator, CategoryId = SeedIds.CategoryBooksStationery, Name = "Calculator", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypePencilCase, CategoryId = SeedIds.CategoryBooksStationery, Name = "Pencil Case", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeWaterBottle, CategoryId = SeedIds.CategoryOther, Name = "Water Bottle", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeLunchBox, CategoryId = SeedIds.CategoryOther, Name = "Lunch Box", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeSportsGear, CategoryId = SeedIds.CategoryOther, Name = "Sports Equipment", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeCharger, CategoryId = SeedIds.CategoryOther, Name = "Charger", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeUniversityId, CategoryId = SeedIds.CategoryIdentityDocuments, Name = "University ID", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeNationalId, CategoryId = SeedIds.CategoryIdentityDocuments, Name = "National ID", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeDrivingLicence, CategoryId = SeedIds.CategoryIdentityDocuments, Name = "Driving Licence", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypePassport, CategoryId = SeedIds.CategoryIdentityDocuments, Name = "Passport", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeOtherElectronics, CategoryId = SeedIds.CategoryElectronics, Name = "Other", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeOtherBags, CategoryId = SeedIds.CategoryBagsWallets, Name = "Other", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeOtherClothing, CategoryId = SeedIds.CategoryClothing, Name = "Other", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeOtherDocuments, CategoryId = SeedIds.CategoryDocumentsCards, Name = "Other", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeOtherKeys, CategoryId = SeedIds.CategoryKeys, Name = "Other", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeOtherJewelry, CategoryId = SeedIds.CategoryJewelryAccessories, Name = "Other", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeOtherBooks, CategoryId = SeedIds.CategoryBooksStationery, Name = "Other", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeOtherOther, CategoryId = SeedIds.CategoryOther, Name = "Other", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp },
            new ItemType { Id = SeedIds.ItemTypeOtherIdentity, CategoryId = SeedIds.CategoryIdentityDocuments, Name = "Other", IsActive = true, CreatedAt = SeedTimestamp, UpdatedAt = SeedTimestamp }
        );
    }
}
