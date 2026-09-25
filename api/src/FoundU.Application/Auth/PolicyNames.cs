namespace FoundU.Application.Auth;

/// <summary>
/// Names of the three authorization policies, shared between FoundU.Infrastructure (where they
/// are defined) and FoundU.Api (where controllers apply them via [Authorize(Policy = ...)]).
/// </summary>
public static class PolicyNames
{
    public const string Student = "Student";
    public const string Staff = "Staff";
    public const string Admin = "Admin";
}
