namespace FoundU.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// Resend API key. Empty means no email is sent (see <see cref="LogWhenUnconfigured"/>).
    /// </summary>
    public string ResendApiKey { get; set; } = string.Empty;

    /// <summary>
    /// With no key, write each email (link included) to the log so the flows can be walked
    /// through locally. Development settings only - the log would hold live reset links.
    /// </summary>
    public bool LogWhenUnconfigured { get; set; }

    /// <summary>The sender, on a domain verified in Resend.</summary>
    public string From { get; set; } = "FoundU <noreply@thejaliya.com>";

    /// <summary>Where the links in emails point - the web app, which has the reset and confirm pages.</summary>
    public string WebBaseUrl { get; set; } = "http://localhost:5173";
}
