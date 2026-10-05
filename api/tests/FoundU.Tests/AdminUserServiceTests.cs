using FoundU.Application.Admin.Dtos;
using FoundU.Application.Admin.Validators;
using FoundU.Application.Common.Exceptions;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Administration;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Tests;

/// <summary>
/// An admin managing accounts: suspending with a reason, reinstating, and changing roles. What
/// matters: nobody can lock themselves or another admin out, a suspension ends the person's
/// sessions, and the reason is long enough to mean something later.
/// </summary>
public sealed class AdminUserServiceTests
{
    // A fresh in-memory database per test, so tests never affect each other.
    private static FoundUDbContext NewDb() => new(new DbContextOptionsBuilder<FoundUDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static AppUser User(string name, UserRole role) => new()
    {
        Id = Guid.NewGuid(), FullName = name, UserName = $"{name}@test", Email = $"{name}@test", Role = role,
    };

    private static async Task<(FoundUDbContext Db, AdminUserService Service, AppUser Admin, AppUser Student)> SetUpAsync()
    {
        var db = NewDb();
        var admin = User("admin", UserRole.Admin);
        var student = User("nadia", UserRole.Student);
        db.Users.AddRange(admin, student);
        await db.SaveChangesAsync();
        return (db, new AdminUserService(db), admin, student);
    }

    [Fact]
    public async Task AnAdminCanSuspendAStudentWithAReason()       // normal
    {
        var (db, service, admin, student) = await SetUpAsync();

        await service.SuspendAsync(student.Id, admin.Id, "Posting fake found items");

        var saved = await db.Users.SingleAsync(u => u.Id == student.Id);
        Assert.True(saved.IsSuspended);
        Assert.Equal("Posting fake found items", saved.SuspensionReason);
        Assert.Equal(admin.Id, saved.SuspendedByUserId);
        Assert.NotNull(saved.SuspendedAt);
    }

    [Fact]
    public async Task AnAdminCannotSuspendThemselves()             // invalid
    {
        var (_, service, admin, _) = await SetUpAsync();

        await Assert.ThrowsAsync<ValidationAppException>(
            () => service.SuspendAsync(admin.Id, admin.Id, "Locking myself out"));
    }

    [Fact]
    public async Task AnotherAdminCannotBeSuspended()              // invalid / security
    {
        var (db, service, admin, _) = await SetUpAsync();
        var otherAdmin = User("second-admin", UserRole.Admin);
        db.Users.Add(otherAdmin);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenAppException>(
            () => service.SuspendAsync(otherAdmin.Id, admin.Id, "Two admins locking each other out"));
    }

    [Fact]
    public async Task SuspendingTwiceIsAConflict()                 // failure
    {
        var (_, service, admin, student) = await SetUpAsync();
        await service.SuspendAsync(student.Id, admin.Id, "Posting fake found items");

        await Assert.ThrowsAsync<ConflictAppException>(
            () => service.SuspendAsync(student.Id, admin.Id, "Posting fake found items"));
    }

    [Fact]
    public async Task SuspendingAnUnknownUserIsNotFound()          // failure
    {
        var (_, service, admin, _) = await SetUpAsync();

        await Assert.ThrowsAsync<NotFoundAppException>(
            () => service.SuspendAsync(Guid.NewGuid(), admin.Id, "Nobody has this id"));
    }

    [Fact]
    public async Task SuspendingRevokesTheUsersRefreshTokens()     // normal / security
    {
        var (db, service, admin, student) = await SetUpAsync();
        db.RefreshTokens.Add(new RefreshToken { UserId = student.Id, TokenHash = "hash", ExpiresAt = DateTime.UtcNow.AddDays(1) });
        await db.SaveChangesAsync();

        await service.SuspendAsync(student.Id, admin.Id, "Posting fake found items");

        var token = await db.RefreshTokens.SingleAsync(t => t.UserId == student.Id);
        Assert.NotNull(token.RevokedAt);
    }

    [Fact]
    public async Task ReinstatingClearsTheSuspension()             // normal
    {
        var (db, service, admin, student) = await SetUpAsync();
        await service.SuspendAsync(student.Id, admin.Id, "Posting fake found items");

        await service.ReinstateAsync(student.Id);

        var saved = await db.Users.SingleAsync(u => u.Id == student.Id);
        Assert.False(saved.IsSuspended);
        Assert.Null(saved.SuspensionReason);
        Assert.Null(saved.SuspendedByUserId);
    }

    [Fact]
    public async Task ReinstatingSomeoneNotSuspendedIsAConflict()  // failure
    {
        var (_, service, _, student) = await SetUpAsync();

        await Assert.ThrowsAsync<ConflictAppException>(() => service.ReinstateAsync(student.Id));
    }

    [Fact]
    public async Task AStudentCanBeMadeStaff()                     // normal
    {
        var (db, service, admin, student) = await SetUpAsync();

        await service.ChangeRoleAsync(student.Id, admin.Id, "Staff");

        Assert.Equal(UserRole.Staff, (await db.Users.SingleAsync(u => u.Id == student.Id)).Role);
    }

    [Fact]
    public async Task AnAdminCannotChangeTheirOwnRole()            // invalid
    {
        var (_, service, admin, _) = await SetUpAsync();

        await Assert.ThrowsAsync<ValidationAppException>(() => service.ChangeRoleAsync(admin.Id, admin.Id, "Student"));
    }

    [Theory]                                                       // invalid
    [InlineData("Superuser")]
    [InlineData("9")]          // a number is not a role name (the Enum.TryParse trap, fix 9)
    [InlineData("")]
    public async Task AnUnknownRoleIsRefused(string role)
    {
        var (_, service, admin, student) = await SetUpAsync();

        await Assert.ThrowsAsync<ValidationAppException>(() => service.ChangeRoleAsync(student.Id, admin.Id, role));
    }

    [Fact]
    public async Task GivingSomeoneTheRoleTheyHaveIsAConflict()    // failure
    {
        var (_, service, admin, student) = await SetUpAsync();

        await Assert.ThrowsAsync<ConflictAppException>(() => service.ChangeRoleAsync(student.Id, admin.Id, "Student"));
    }

    [Theory]                                                       // boundary
    [InlineData(0, false)]     // empty
    [InlineData(9, false)]     // one below the minimum
    [InlineData(10, true)]     // exactly the minimum
    [InlineData(500, true)]    // exactly the maximum
    [InlineData(501, false)]   // one above the maximum
    public void TheSuspensionReasonMustBe10To500Characters(int length, bool valid)
    {
        var result = new SuspendUserRequestValidator().Validate(new SuspendUserRequest(new string('a', length)));

        Assert.Equal(valid, result.IsValid);
    }
}
