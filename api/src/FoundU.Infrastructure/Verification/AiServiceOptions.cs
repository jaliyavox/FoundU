namespace FoundU.Infrastructure.Verification;

public sealed class AiServiceOptions
{
    public const string SectionName = "AiService";

    public string BaseUrl { get; init; } = "http://localhost:8000";
    public int TimeoutSeconds { get; init; } = 5;
}
