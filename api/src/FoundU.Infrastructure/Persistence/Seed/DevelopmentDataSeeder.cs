using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FoundU.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds a local development/demo Admin account through UserManager, so the password goes
/// through Identity's own PasswordHasher&lt;AppUser&gt; (never a hand-rolled hash). Deliberately
/// kept OUT of EF Core's HasData (migration-baked seeding), because HasData runs unconditionally
/// in every environment including production.
///
/// Call this only when the hosting environment is Development, e.g. in Program.cs:
///
///   if (app.Environment.IsDevelopment())
///   {
///       using var scope = app.Services.CreateScope();
///       await DevelopmentDataSeeder.SeedAsync(
///           scope.ServiceProvider.GetRequiredService&lt;UserManager&lt;AppUser&gt;&gt;(),
///           scope.ServiceProvider.GetRequiredService&lt;FoundUDbContext&gt;(),
///           scope.ServiceProvider.GetRequiredService&lt;IConfiguration&gt;());
///   }
///
/// The admin password comes from configuration/environment variables
/// (Seed:DevAdminPassword or DEV_ADMIN_PASSWORD), never a hardcoded hash.
/// </summary>
public static class DevelopmentDataSeeder
{
    /// <param name="allowFallbackPassword">
    /// Development only. Anywhere else the admin password must be configured: a deployed
    /// server with a password printed in the source is an open door.
    /// </param>
    public static async Task SeedAsync(
        UserManager<AppUser> userManager,
        FoundUDbContext db,
        IConfiguration configuration,
        bool allowFallbackPassword = true)
    {
        await db.Database.MigrateAsync();

        var alreadySeeded = await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Role == UserRole.Admin);
        if (alreadySeeded)
        {
            return;
        }

        var configured = configuration["Seed:DevAdminPassword"]
            ?? Environment.GetEnvironmentVariable("DEV_ADMIN_PASSWORD");
        if (string.IsNullOrWhiteSpace(configured) && !allowFallbackPassword)
            throw new InvalidOperationException(
                "Set Seed__DevAdminPassword before the first start: it becomes the admin account's password.");
        var devPassword = string.IsNullOrWhiteSpace(configured)
            ? "DevOnly-ChangeMe-123!" // clearly-labelled fallback, dev environments only
            : configured;

        const string email = "admin@foundu.com";

        var admin = new AppUser
        {
            Id = SeedIds.AdminUserId,
            UserName = email, // UserName == Email by convention - see AppUser.cs
            Email = email,
            EmailConfirmed = true,
            FullName = "FoundU Dev Administrator",
            Role = UserRole.Admin,
            IsSuspended = false,
            IsDeleted = false
        };

        var result = await userManager.CreateAsync(admin, devPassword);

        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to seed development Admin account: {errors}");
        }

        if (!await db.Categories.AnyAsync())
        {
            var electronics = new Category { Id = Guid.NewGuid(), Name = "Electronics", Description = "Laptops, phones, chargers, audio devices" };
            var bags = new Category { Id = Guid.NewGuid(), Name = "Bags & Wallets", Description = "Backpacks, wallets, purses, pouches" };
            var keys = new Category { Id = Guid.NewGuid(), Name = "Keys & Cards", Description = "Keys, keychains, student IDs, bank cards" };
            var clothing = new Category { Id = Guid.NewGuid(), Name = "Clothing & Accessories", Description = "Jackets, hats, glasses, umbrellas" };

            db.Categories.AddRange(electronics, bags, keys, clothing);

            db.ItemTypes.AddRange(
                new ItemType { Id = Guid.NewGuid(), CategoryId = electronics.Id, Name = "Laptop" },
                new ItemType { Id = Guid.NewGuid(), CategoryId = electronics.Id, Name = "Smartphone" },
                new ItemType { Id = Guid.NewGuid(), CategoryId = electronics.Id, Name = "Headphones / Earbuds" },
                new ItemType { Id = Guid.NewGuid(), CategoryId = electronics.Id, Name = "Charger / Adapter" },
                new ItemType { Id = Guid.NewGuid(), CategoryId = bags.Id, Name = "Backpack" },
                new ItemType { Id = Guid.NewGuid(), CategoryId = bags.Id, Name = "Wallet" },
                new ItemType { Id = Guid.NewGuid(), CategoryId = keys.Id, Name = "Keys" },
                new ItemType { Id = Guid.NewGuid(), CategoryId = keys.Id, Name = "Student ID Card" },
                new ItemType { Id = Guid.NewGuid(), CategoryId = clothing.Id, Name = "Jacket" },
                new ItemType { Id = Guid.NewGuid(), CategoryId = clothing.Id, Name = "Water Bottle" }
            );

            db.CampusLocations.AddRange(
                new CampusLocation { Id = Guid.NewGuid(), Name = "Main Library", Building = "Library Hall" },
                new CampusLocation { Id = Guid.NewGuid(), Name = "Student Center", Building = "Building A" },
                new CampusLocation { Id = Guid.NewGuid(), Name = "Engineering Complex", Building = "Tech Block B" },
                new CampusLocation { Id = Guid.NewGuid(), Name = "Cafeteria", Building = "Student Union" },
                new CampusLocation { Id = Guid.NewGuid(), Name = "Sports Center Gym", Building = "Athletic Annex" }
            );

            await db.SaveChangesAsync();
        }
    }
}
