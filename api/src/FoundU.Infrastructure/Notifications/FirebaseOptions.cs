namespace FoundU.Infrastructure.Notifications;

/// <summary>Backend-only Firebase Admin configuration. No credential is stored in source control.</summary>
public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";

    public string? ProjectId { get; init; }
    public string? CredentialsPath { get; init; }
    public int TimeoutSeconds { get; init; } = 5;
}
