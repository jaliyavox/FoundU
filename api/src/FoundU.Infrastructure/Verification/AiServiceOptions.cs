namespace FoundU.Infrastructure.Verification;

public sealed class AiServiceOptions
{
    public const string SectionName = "AiService";
    public const string ServiceKeyHeaderName = "X-FoundU-Service-Key";

    public string BaseUrl { get; init; } = "http://localhost:8000";
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Ping the AI service when the API starts and every few minutes while it runs. On a host
    /// that puts idle services to sleep (Render's free plan) the AI service otherwise takes
    /// longer to wake than a request waits, and the first question after a quiet spell fails.
    /// The pings stop when the API itself sleeps, so the two never stay up around the clock.
    /// </summary>
    public bool KeepWarm { get; init; } = true;
    public string ServiceKey { get; init; } = string.Empty;

    public static string RequireServiceKey(AiServiceOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ServiceKey)
            || options.ServiceKey.Length < 32
            || options.ServiceKey.Any(char.IsControl)
            || options.ServiceKey.Contains("replace", StringComparison.OrdinalIgnoreCase)
            || options.ServiceKey.Contains("placeholder", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("AI service configuration is invalid.");
        }

        return options.ServiceKey;
    }
}
