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
    public static async Task SeedAsync(UserManager<AppUser> userManager, FoundUDbContext db, IConfiguration configuration)
    {
        var alreadySeeded = await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Role == UserRole.Admin);
        if (alreadySeeded)
        {
            return;
        }

        var devPassword = configuration["Seed:DevAdminPassword"]
            ?? Environment.GetEnvironmentVariable("DEV_ADMIN_PASSWORD")
            ?? "DevOnly-ChangeMe-123!"; // clearly-labelled fallback, dev environments only

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
    }
}
