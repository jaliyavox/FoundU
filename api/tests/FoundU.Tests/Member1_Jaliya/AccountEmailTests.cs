using System.Text.RegularExpressions;
using FoundU.Application.Abstractions;
using FoundU.Application.Auth.Dtos;
using FoundU.Application.Common.Exceptions;
using FoundU.Application.Email;
using FoundU.Domain.Entities;
using FoundU.Infrastructure;
using FoundU.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FoundU.Tests;

/// <summary>
/// Forgot password and email confirmation, with real Identity tokens and a mailbox standing in
/// for Resend. The rules that matter: the form never reveals who has an account; a reset link
/// works once and ends every other session; a confirmation link proves the address.
/// </summary>
[Trait("Member", "Member1-Jaliya")]
public sealed class AccountEmailTests
{
    [Fact]
    public async Task SigningUpSendsAConfirmationLinkThatConfirmsTheAddress()
    {
        await using var app = await App.CreateAsync();
        await app.Auth.RegisterAsync(new("New Student", "new@test.com", "Password123", null), "127.0.0.1");

        var mail = Assert.Single(app.Mailbox.Sent);
        Assert.Equal("new@test.com", mail.To);
        var (userId, token) = ConfirmLink(mail.Text);

        var user = await app.Users.FindByEmailAsync("new@test.com");
        Assert.False(user!.EmailConfirmed);

        await app.Emails.ConfirmEmailAsync(new ConfirmEmailRequest(userId, token));

        Assert.True((await app.Users.FindByEmailAsync("new@test.com"))!.EmailConfirmed);
    }

    [Fact]
    public async Task AResetLinkSetsTheNewPasswordOnceAndEndsOtherSessions()
    {
        await using var app = await App.CreateAsync();
        var signedIn = await app.Auth.RegisterAsync(new("Student", "forgetful@test.com", "Password123", null), "127.0.0.1");
        app.Mailbox.Sent.Clear();

        await app.Emails.RequestPasswordResetAsync("forgetful@test.com");
        var token = ResetToken(Assert.Single(app.Mailbox.Sent).Text);

        await app.Emails.ResetPasswordAsync(new ResetPasswordRequest("forgetful@test.com", token, "NewPassword456"), "127.0.0.1");

        var user = await app.Users.FindByEmailAsync("forgetful@test.com");
        Assert.True(await app.Users.CheckPasswordAsync(user!, "NewPassword456"));
        Assert.False(await app.Users.CheckPasswordAsync(user!, "Password123"));
        // The inbox was reached, which proves the address.
        Assert.True(user!.EmailConfirmed);
        // The session opened before the reset no longer refreshes.
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => app.Auth.RefreshAsync(signedIn.RefreshToken, "127.0.0.1"));

        // The same link a second time is spent.
        var again = await Assert.ThrowsAsync<ValidationAppException>(() =>
            app.Emails.ResetPasswordAsync(new ResetPasswordRequest("forgetful@test.com", token, "Another789x"), "127.0.0.1"));
        Assert.Contains("expired or was already used", string.Join(" ", again.Errors.SelectMany(e => e.Value)));
    }

    [Fact]
    public async Task TheFormSaysNothingAboutWhoHasAnAccount()
    {
        await using var app = await App.CreateAsync();

        // No exception, no email - the caller cannot tell this from a real account.
        await app.Emails.RequestPasswordResetAsync("nobody@test.com");

        Assert.Empty(app.Mailbox.Sent);
    }

    [Fact]
    public async Task ResetEmailsAreThrottled()
    {
        await using var app = await App.CreateAsync();
        await app.Auth.RegisterAsync(new("Student", "busy@test.com", "Password123", null), "127.0.0.1");
        app.Mailbox.Sent.Clear();

        await app.Emails.RequestPasswordResetAsync("busy@test.com");
        await app.Emails.RequestPasswordResetAsync("busy@test.com");
        await app.Emails.RequestPasswordResetAsync("busy@test.com");

        Assert.Single(app.Mailbox.Sent);
    }

    [Fact]
    public async Task ATamperedLinkIsRefused()
    {
        await using var app = await App.CreateAsync();
        await app.Auth.RegisterAsync(new("Student", "safe@test.com", "Password123", null), "127.0.0.1");

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            app.Emails.ResetPasswordAsync(new ResetPasswordRequest("safe@test.com", "bm90LWEtcmVhbC10b2tlbg", "NewPassword456"), null));
        await Assert.ThrowsAsync<ValidationAppException>(() =>
            app.Emails.ResetPasswordAsync(new ResetPasswordRequest("safe@test.com", "%%%not base64%%%", "NewPassword456"), null));
    }

    private static (Guid UserId, string Token) ConfirmLink(string text)
    {
        var match = Regex.Match(text, @"confirm-email\?userId=([0-9a-f-]+)&token=([A-Za-z0-9_-]+)");
        Assert.True(match.Success, "no confirmation link in the email");
        return (Guid.Parse(match.Groups[1].Value), match.Groups[2].Value);
    }

    private static string ResetToken(string text)
    {
        var match = Regex.Match(text, @"reset-password\?email=[^&\s]+&token=([A-Za-z0-9_-]+)");
        Assert.True(match.Success, "no reset link in the email");
        return match.Groups[1].Value;
    }

    private sealed class Mailbox : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);
            return Task.FromResult(true);
        }
    }

    private sealed class App : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly AsyncServiceScope _scope;

        private App(ServiceProvider provider, AsyncServiceScope scope, Mailbox mailbox)
            => (_provider, _scope, Mailbox) = (provider, scope, mailbox);

        public Mailbox Mailbox { get; }
        public IAuthService Auth => _scope.ServiceProvider.GetRequiredService<IAuthService>();
        public IAccountEmailService Emails => _scope.ServiceProvider.GetRequiredService<IAccountEmailService>();
        public UserManager<AppUser> Users => _scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        public static async Task<App> CreateAsync()
        {
            var mailbox = new Mailbox();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpContextAccessor();
            services.AddFoundUInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(AuthTestApp.Settings).Build());
            AuthTestApp.ReplaceDatabase(services, $"foundu-email-{Guid.NewGuid():N}");
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(mailbox);
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<FoundUDbContext>().Database.EnsureCreatedAsync();
            return new App(provider, scope, mailbox);
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}

[Trait("Member", "Member1-Jaliya")]
public sealed class UndeliverableAddressTests
{
    [Theory]
    [InlineData("amara@foundu.test", true)]
    [InlineData("someone@example.com", true)]
    [InlineData("x@thing.invalid", true)]
    [InlineData("x@localhost", true)]
    [InlineData("student@gmail.com", false)]
    [InlineData("me@thejaliya.com", false)]
    [InlineData("me@contest.lk", false)]
    public void ReservedTestDomainsAreNeverMailed(string address, bool undeliverable)
        => Assert.Equal(undeliverable, FoundU.Infrastructure.Email.ResendEmailSender.IsUndeliverable(address));
}
