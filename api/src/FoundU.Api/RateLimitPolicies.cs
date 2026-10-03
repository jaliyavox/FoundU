namespace FoundU.Api;

/// <summary>Names of the rate-limiting policies registered in Program.cs.</summary>
public static class RateLimitPolicies
{
    /// <summary>Endpoints that send an email: a few per minute from any one address.</summary>
    public const string AccountEmail = "account-email";
}
