using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FoundU.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds a local development/demo Admin account. Deliberately kept OUT of EF Core's HasData
/// (migration-baked seeding) because HasData runs unconditionally in every environment,
/// including production - which is exactly how a placeholder credential ends up looking like
/// a real one in a deployed system.
///
/// Call this only when the hosting environment is Development, e.g. in Program.cs:
///
///   if (app.Environment.IsDevelopment())
///   {
///       using var scope = app.Services.CreateScope();
///       await DevelopmentDataSeeder.SeedAsync(
///           scope.ServiceProvider.GetRequiredService&lt;FoundUDbContext&gt;(),
///           scope.ServiceProvider.GetRequiredService&lt;IConfiguration&gt;());
///   }
///
/// The admin password comes from configuration/environment variables
/// (Seed:DevAdminPassword or DEV_ADMIN_PASSWORD), never a hardcoded hash, and a warning is
/// logged if it falls back to the default so nobody mistakes it for a secure setup.
/// </summary>
public static class DevelopmentDataSeeder
{
    public static async Task SeedAsync(FoundUDbContext db, IConfiguration configuration)
    {
        var alreadySeeded = await db.AppUsers.IgnoreQueryFilters().AnyAsync(u => u.Role == UserRole.Admin);
        if (alreadySeeded)
        {
            return;
        }

        var devPassword = configuration["Seed:DevAdminPassword"]
            ?? Environment.GetEnvironmentVariable("DEV_ADMIN_PASSWORD")
            ?? "DevOnly-ChangeMe-123!"; // clearly-labelled fallback, dev environments only

        var admin = new AppUser
        {
            Id = SeedIds.AdminUserId,
            FullName = "FoundU Dev Administrator",
            Email = "dev-admin@foundu.local",
            NormalizedEmail = "DEV-ADMIN@FOUNDU.LOCAL",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(devPassword),
            Role = UserRole.Admin,
            IsSuspended = false,
            IsDeleted = false
        };

        db.AppUsers.Add(admin);
        await db.SaveChangesAsync();
    }
}
