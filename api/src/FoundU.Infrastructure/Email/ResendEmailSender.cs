using System.Net.Http.Headers;
using System.Net.Http.Json;
using FoundU.Application.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FoundU.Infrastructure.Email;

/// <summary>
/// Sends through Resend's HTTP API. Never throws for a delivery problem: a reset email that
/// failed must not turn into a 500 that tells the caller the address exists.
/// </summary>
public sealed class ResendEmailSender(
    HttpClient http,
    IOptions<EmailOptions> options,
    ILogger<ResendEmailSender> logger) : IEmailSender
{
    /// <summary>
    /// Reserved names that can never receive mail (RFC 2606/6761) - the demo accounts live on
    /// foundu.test. Sending there only bounces, and bounces cost the sending domain its
    /// reputation, so these are skipped before they reach Resend.
    /// </summary>
    public static bool IsUndeliverable(string address)
    {
        var domain = address[(address.LastIndexOf('@') + 1)..].Trim().TrimEnd('.').ToLowerInvariant();
        string[] reservedTlds = [".test", ".example", ".invalid", ".localhost"];
        string[] reservedDomains = ["example.com", "example.net", "example.org"];
        return reservedTlds.Any(tld => domain.EndsWith(tld, StringComparison.Ordinal) || domain == tld[1..])
            || reservedDomains.Contains(domain);
    }

    public async Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (IsUndeliverable(message.To))
        {
            logger.LogInformation("Email not sent: {Domain} is a reserved test domain.", message.To[(message.To.LastIndexOf('@') + 1)..]);
            return false;
        }
        if (string.IsNullOrWhiteSpace(settings.ResendApiKey))
        {
            if (settings.LogWhenUnconfigured)
            {
                // Local walk-through without a key. The body holds a live link - see EmailOptions.
                logger.LogWarning("Email not sent (no Resend key). To {To}: {Subject}\n{Text}", message.To, message.Subject, message.Text);
            }
            else
            {
                logger.LogError("Email not sent: Email:ResendApiKey is not configured.");
            }
            return false;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
            {
                Content = JsonContent.Create(new
                {
                    from = settings.From,
                    to = new[] { message.To },
                    subject = message.Subject,
                    html = message.Html,
                    text = message.Text,
                }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ResendApiKey);

            using var response = await http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode) return true;

            // Resend explains itself (unverified domain, bad key); the reason is worth the log.
            // The recipient's address and the email body are not.
            var reason = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Resend refused an email ({Status}): {Reason}", (int)response.StatusCode, reason.Length > 300 ? reason[..300] : reason);
            return false;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogError("Could not reach Resend: {Reason}", exception.GetType().Name);
            return false;
        }
    }
}
