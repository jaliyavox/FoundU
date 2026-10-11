using FoundU.Application.Notifications.Dtos;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using static FoundU.Tests.PostgresTestSupport;

namespace FoundU.Tests;

/// <summary>User accounts and their push-notification devices, on real PostgreSQL.</summary>
[Trait("Category", "PostgreSql")]
[Trait("Member", "Member1-Jaliya")]
public sealed class Member1DatabaseTests
{
    [PostgresFact]
    public async Task DeviceTokenUniqueIndexIsEnforcedByPostgreSql()
    {
        await using var db = PostgresTestDatabase.CreateContext();
        await PostgresTestDatabase.MigrateAsync(db);
        var suffix = Guid.NewGuid().ToString("N");
        var first = new AppUser { UserName = $"pg-{suffix}@test.invalid", Email = $"pg-{suffix}@test.invalid", FullName = "Postgres Test", Role = UserRole.Student };
        var second = new AppUser { UserName = $"pg2-{suffix}@test.invalid", Email = $"pg2-{suffix}@test.invalid", FullName = "Postgres Test Two", Role = UserRole.Student };
        db.Users.AddRange(first, second);
        await db.SaveChangesAsync();
        db.DeviceRegistrations.Add(new DeviceRegistration { UserId = first.Id, FcmToken = $"test-token-{suffix}", Platform = "android" });
        await db.SaveChangesAsync();
        db.DeviceRegistrations.Add(new DeviceRegistration { UserId = second.Id, FcmToken = $"test-token-{suffix}", Platform = "android" });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task DeviceReregistrationMovesTokenAndUnregisterDeactivatesIt()
    {
        await using var db = PostgresTestDatabase.CreateContext();
        await PostgresTestDatabase.MigrateAsync(db);
        var first = NewUser("device-first");
        var second = NewUser("device-second");
        db.Users.AddRange(first, second);
        await db.SaveChangesAsync();
        var service = new DeviceRegistrationService(db);
        var token = $"test-device-token-{Guid.NewGuid():N}";

        await service.RegisterAsync(first.Id, new RegisterDeviceRequest(token, "android"));
        await service.RegisterAsync(second.Id, new RegisterDeviceRequest(token, "ios"));
        await service.UnregisterAsync(second.Id, new UnregisterDeviceRequest(token));
        db.ChangeTracker.Clear();

        var registration = await db.DeviceRegistrations.SingleAsync(item => item.FcmToken == token);
        Assert.Equal(second.Id, registration.UserId);
        Assert.False(registration.IsActive);
        Assert.NotNull(registration.DeactivatedAt);
    }
}
