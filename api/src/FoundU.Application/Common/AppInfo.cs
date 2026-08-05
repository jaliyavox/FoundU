namespace FoundU.Application.Common;

/// <summary>
/// Placeholder application-layer type so the skeleton has something testable.
/// Application services (reporting, claims, notifications, admin) land here in later steps.
/// </summary>
public static class AppInfo
{
    public const string ProjectName = "FoundU";

    public static string Describe() => $"{ProjectName} API";
}
