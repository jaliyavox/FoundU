using System.Net;
using System.Text;
using FoundU.Application.Common.Exceptions;
using FoundU.Application.Email;
using FoundU.Domain.Entities;
using FoundU.Infrastructure.Email;
using FoundU.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FoundU.Infrastructure.Identity;

/// <summary>
/// Reset and confirmation emails. The tokens are ASP.NET Identity's own: single-purpose,
/// time-limited, and tied to the account's security stamp, so a password change voids every
/// reset link sent before it.
/// </summary>
public sealed class AccountEmailService(
    UserManager<AppUser> userManager,
    FoundUDbContext db,
    IEmailSender email,
    IMemoryCache cache,
    IOptions<EmailOptions> options) : IAccountEmailService
{
    /// <summary>One email of each kind per account per minute - enough to retry, not to spam an inbox.</summary>
    private static readonly TimeSpan Throttle = TimeSpan.FromMinutes(1);

    public async Task RequestPasswordResetAsync(string address, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(address.Trim());
        // Unknown address, suspended account, or one sent a moment ago: the same silence, so
        // nobody learns who has an account from how this answers.
        if (user is null || user.IsSuspended || !TryTake($"reset:{user.Id}")) return;

        var token = Encode(await userManager.GeneratePasswordResetTokenAsync(user));
        var link = $"{Base}/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={token}";

        await email.SendAsync(Compose(
            user.Email!,
            "Reset your FoundU password",
            $"Hi {First(user)},",
            "Someone - hopefully you - asked to reset the password for your FoundU account. The link works once and expires in a day.",
            "Choose a new password",
            link,
            "If this wasn't you, ignore this email: your password stays as it is."), cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim())
            ?? throw ExpiredLink();

        var result = await userManager.ResetPasswordAsync(user, Decode(request.Token) ?? throw ExpiredLink(), request.NewPassword);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken)))
                throw ExpiredLink();
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["NewPassword"] = result.Errors.Select(e => e.Description).ToArray(),
            });
        }

        // The link reached their inbox, which is what confirming an address proves anyway.
        // A forgotten password often follows a lockout; the reset ends that too.
        user.EmailConfirmed = true;
        await userManager.UpdateAsync(user);
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);

        // Every session elsewhere was opened with the old password - end them.
        var sessions = await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.RevokedAt = DateTime.UtcNow;
            session.RevokedByIp = ipAddress;
            session.ReasonRevoked = "Password reset";
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SendConfirmationAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundAppException("Account not found.");
        if (user.EmailConfirmed) return;
        if (!TryTake($"confirm:{user.Id}"))
            throw new ConflictAppException("We sent one a moment ago - give it a minute, and check your spam folder.");

        var token = Encode(await userManager.GenerateEmailConfirmationTokenAsync(user));
        var link = $"{Base}/confirm-email?userId={user.Id}&token={token}";

        await email.SendAsync(Compose(
            user.Email!,
            "Confirm your email for FoundU",
            $"Hi {First(user)},",
            "Confirm this is your address, so we can reach you when something of yours turns up - and so you can reset your password if you ever forget it.",
            "Confirm my email",
            link,
            "If you didn't create a FoundU account, ignore this email."), cancellationToken);
    }

    public async Task ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString())
            ?? throw ExpiredLink();
        if (user.EmailConfirmed) return;

        var result = await userManager.ConfirmEmailAsync(user, Decode(request.Token) ?? throw ExpiredLink());
        if (!result.Succeeded) throw ExpiredLink();
    }

    private string Base => options.Value.WebBaseUrl.TrimEnd('/');

    private bool TryTake(string key)
    {
        if (cache.TryGetValue(key, out _)) return false;
        cache.Set(key, true, Throttle);
        return true;
    }

    private static ValidationAppException ExpiredLink() =>
        new("Token", "That link has expired or was already used. Ask for a new one.");

    // Identity tokens contain characters that do not survive a URL; base64url does.
    private static string Encode(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    private static string? Decode(string token)
    {
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token.Trim()));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string First(AppUser user) =>
        WebUtility.HtmlEncode(user.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "there");

    /// <summary>A short branded email: greeting, one paragraph, one button, one line of reassurance.</summary>
    private static EmailMessage Compose(string to, string subject, string greeting, string body, string action, string link, string footnote)
    {
        var safeLink = WebUtility.HtmlEncode(link);
        var html = $"""
            <!doctype html>
            <html><body style="margin:0;background:#f3f7f2;font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#13261a">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="padding:32px 16px">
                <tr><td align="center">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:480px;background:#ffffff;border-radius:16px;padding:32px">
                    <tr><td style="font-size:20px;font-weight:700;color:#1f5d20;padding-bottom:20px">FoundU</td></tr>
                    <tr><td style="font-size:16px;padding-bottom:8px">{greeting}</td></tr>
                    <tr><td style="font-size:15px;line-height:1.55;color:#3c4f43;padding-bottom:24px">{WebUtility.HtmlEncode(body)}</td></tr>
                    <tr><td style="padding-bottom:24px">
                      <a href="{safeLink}" style="display:inline-block;background:#1f5d20;color:#ffffff;text-decoration:none;font-weight:600;padding:12px 22px;border-radius:999px">{WebUtility.HtmlEncode(action)}</a>
                    </td></tr>
                    <tr><td style="font-size:13px;line-height:1.5;color:#6b7c70;padding-bottom:12px">{WebUtility.HtmlEncode(footnote)}</td></tr>
                    <tr><td style="font-size:12px;line-height:1.5;color:#8a998f;word-break:break-all">Button not working? Paste this into your browser:<br>{safeLink}</td></tr>
                  </table>
                  <p style="font-size:12px;color:#8a998f;margin-top:16px">Campus lost &amp; found · sent from a no-reply address</p>
                </td></tr>
              </table>
            </body></html>
            """;
        var plainGreeting = WebUtility.HtmlDecode(greeting);
        var text = $"{plainGreeting}\n\n{body}\n\n{action}: {link}\n\n{footnote}\n\n- FoundU (no-reply)";
        return new EmailMessage(to, subject, html, text);
    }
}
