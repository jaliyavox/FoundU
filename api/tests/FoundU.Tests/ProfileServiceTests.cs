using FoundU.Application.Abstractions;
using FoundU.Application.Auth.Dtos;
using FoundU.Application.Common.Exceptions;
using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FoundU.Tests;

/// <summary>
/// Editing your own account. The rules worth pinning: the email address is guarded by the
/// password, a password change ends every other session, and nobody edits anyone else.
/// </summary>
public sealed class ProfileServiceTests
{
    private const string Password = "Password123";

    [Fact]
    public async Task NameAndStudentNumberChangeWithoutAPassword()
    {
        await using var app = await AuthTestApp.CreateAsync();
        var signedIn = await app.RegisterAsync("edit.me@foundu.test");
        var profile = app.Services.GetRequiredService<IProfileService>();

        var updated = await profile.UpdateAsync(
            signedIn.User.Id,
            new UpdateProfileRequest("Jaliya Hettiarachchi", "edit.me@foundu.test", "IT26001234", null));

        Assert.Equal("Jaliya Hettiarachchi", updated.FullName);
        Assert.Equal("IT26001234", updated.StudentNumber);
        Assert.True(updated.HasPassword);
        Assert.False(updated.IsGoogleLinked);
    }

    [Fact]
    public async Task ChangingTheEmailNeedsTheCurrentPassword()
    {
        await using var app = await AuthTestApp.CreateAsync();
        var signedIn = await app.RegisterAsync("old.address@foundu.test");
        var profile = app.Services.GetRequiredService<IProfileService>();

        var missing = await Assert.ThrowsAsync<ValidationAppException>(() => profile.UpdateAsync(
            signedIn.User.Id,
            new UpdateProfileRequest("Test Student", "new.address@foundu.test", null, null)));
        Assert.True(missing.Errors.ContainsKey("CurrentPassword"));

        var wrong = await Assert.ThrowsAsync<ValidationAppException>(() => profile.UpdateAsync(
            signedIn.User.Id,
            new UpdateProfileRequest("Test Student", "new.address@foundu.test", null, "not-the-password")));
        Assert.True(wrong.Errors.ContainsKey("CurrentPassword"));

        // Still the old address, and still able to sign in with it.
        var unchanged = await app.Users.FindByIdAsync(signedIn.User.Id.ToString());
        Assert.Equal("old.address@foundu.test", unchanged!.Email);
    }

    [Fact]
    public async Task TheRightPasswordMovesTheEmailAndTheSignInNameWithIt()
    {
        await using var app = await AuthTestApp.CreateAsync();
        var signedIn = await app.RegisterAsync("before@foundu.test");
        var profile = app.Services.GetRequiredService<IProfileService>();

        await profile.UpdateAsync(
            signedIn.User.Id,
            new UpdateProfileRequest("Test Student", "after@foundu.test", null, Password));

        var user = await app.Users.FindByIdAsync(signedIn.User.Id.ToString());
        Assert.Equal("after@foundu.test", user!.Email);
        // UserName == Email by convention, so signing in with the new address must work.
        Assert.Equal("after@foundu.test", user.UserName);
        var again = await app.Auth.LoginAsync(new("after@foundu.test", Password), null);
        Assert.Equal(signedIn.User.Id, again.User.Id);
    }

    [Fact]
    public async Task AnEmailSomebodyElseAlreadyUsesIsRefused()
    {
        await using var app = await AuthTestApp.CreateAsync();
        var mine = await app.RegisterAsync("mine@foundu.test");
        await app.RegisterAsync("theirs@foundu.test");
        var profile = app.Services.GetRequiredService<IProfileService>();

        await Assert.ThrowsAsync<ConflictAppException>(() => profile.UpdateAsync(
            mine.User.Id,
            new UpdateProfileRequest("Test Student", "theirs@foundu.test", null, Password)));
    }

    [Fact]
    public async Task ChangingThePasswordEndsEveryOtherSessionAndKeepsThisOne()
    {
        await using var app = await AuthTestApp.CreateAsync();
        var first = await app.RegisterAsync("sessions@foundu.test");
        // A second device signs in: its refresh token must stop working after the change.
        var second = await app.Auth.LoginAsync(new("sessions@foundu.test", Password), null);
        var profile = app.Services.GetRequiredService<IProfileService>();

        var fresh = await profile.ChangePasswordAsync(
            first.User.Id,
            new ChangePasswordRequest(Password, "BrandNewPassword456"),
            "127.0.0.1");

        Assert.NotEqual(first.RefreshToken, fresh.RefreshToken);
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => app.Auth.RefreshAsync(second.RefreshToken, null));
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => app.Auth.RefreshAsync(first.RefreshToken, null));

        // The pair handed back still works, so the person who made the change stays in.
        var rotated = await app.Auth.RefreshAsync(fresh.RefreshToken, null);
        Assert.Equal(first.User.Id, rotated.User.Id);

        // And the new password is the one that works from now on.
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => app.Auth.LoginAsync(new("sessions@foundu.test", Password), null));
        Assert.Equal(first.User.Id, (await app.Auth.LoginAsync(new("sessions@foundu.test", "BrandNewPassword456"), null)).User.Id);
    }

    [Fact]
    public async Task TheWrongCurrentPasswordChangesNothing()
    {
        await using var app = await AuthTestApp.CreateAsync();
        var signedIn = await app.RegisterAsync("guarded@foundu.test");
        var profile = app.Services.GetRequiredService<IProfileService>();

        await Assert.ThrowsAsync<ValidationAppException>(() => profile.ChangePasswordAsync(
            signedIn.User.Id,
            new ChangePasswordRequest("wrong-password", "BrandNewPassword456"),
            null));

        // The old password still works and the session was not thrown away.
        Assert.Equal(signedIn.User.Id, (await app.Auth.LoginAsync(new("guarded@foundu.test", Password), null)).User.Id);
        Assert.Equal(signedIn.User.Id, (await app.Auth.RefreshAsync(signedIn.RefreshToken, null)).User.Id);
    }

    [Fact]
    public async Task AnAccountCreatedThroughGoogleSetsItsFirstPasswordWithoutOne()
    {
        await using var app = await AuthTestApp.CreateAsync();
        var user = new AppUser
        {
            UserName = "google.person@foundu.test",
            Email = "google.person@foundu.test",
            FullName = "Google Person",
            EmailConfirmed = true,
            GoogleSubjectId = "google-subject-1",
        };
        Assert.True((await app.Users.CreateAsync(user)).Succeeded);
        var profile = app.Services.GetRequiredService<IProfileService>();

        var before = await profile.GetAsync(user.Id);
        Assert.False(before.HasPassword);
        Assert.True(before.IsGoogleLinked);

        await profile.ChangePasswordAsync(user.Id, new ChangePasswordRequest(null, "FirstPassword123"), null);

        Assert.True((await profile.GetAsync(user.Id)).HasPassword);
        Assert.Equal(user.Id, (await app.Auth.LoginAsync(new("google.person@foundu.test", "FirstPassword123"), null)).User.Id);
    }

    [Fact]
    public async Task GoogleSignInIsRefusedWhenTheServerHasNoClientId()
    {
        await using var app = await AuthTestApp.CreateAsync();

        // The test app configures no Google:ClientId, which is the default everywhere until
        // somebody sets one - the endpoint must say so rather than half-working.
        await Assert.ThrowsAsync<ConflictAppException>(
            () => app.Auth.GoogleSignInAsync(new GoogleSignInRequest("any.token.value"), null));

        Assert.False(await app.Db.Users.AnyAsync(u => u.GoogleSubjectId != null));
    }
}
