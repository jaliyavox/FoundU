namespace FoundU.Infrastructure.Identity;

/// <summary>Bound from configuration section "Jwt". SigningKey belongs in User Secrets or an environment variable, never committed configuration.</summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = default!;
    public string Audience { get; set; } = default!;
    public string SigningKey { get; set; } = default!;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 14;
}
