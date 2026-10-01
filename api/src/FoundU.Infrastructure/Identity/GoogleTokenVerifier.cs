using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FoundU.Application.Abstractions;
using FoundU.Application.Auth.Dtos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FoundU.Infrastructure.Identity;

/// <summary>
/// Checks a Google ID token the way Google asks: verify the signature against their published
/// keys, then the issuer, the audience and the expiry, and only then read the claims.
///
/// Anything that does not check out returns null. There is no "probably fine" branch, and no
/// detail from a rejected token reaches the caller or the logs.
/// </summary>
public sealed class GoogleTokenVerifier : IGoogleTokenVerifier
{
    private const string CacheKey = "google:jwks";
    private static readonly string[] ValidIssuers = ["accounts.google.com", "https://accounts.google.com"];

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly GoogleAuthOptions _options;
    private readonly ILogger<GoogleTokenVerifier> _logger;

    public GoogleTokenVerifier(
        HttpClient httpClient,
        IMemoryCache cache,
        IOptions<GoogleAuthOptions> options,
        ILogger<GoogleTokenVerifier> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ClientId);

    public async Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return null;

        try
        {
            var keys = await GetSigningKeysAsync(cancellationToken);
            if (keys is null) return null;

            var handler = new JwtSecurityTokenHandler();
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuers = ValidIssuers,
                ValidateAudience = true,
                ValidAudience = _options.ClientId,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = keys,
                ClockSkew = TimeSpan.FromMinutes(2),
            };

            var principal = handler.ValidateToken(idToken, parameters, out _);

            var subject = principal.FindFirstValue("sub");
            var email = principal.FindFirstValue("email");
            // Google says whether it has confirmed the address. An unconfirmed one must not
            // be allowed to take over an existing FoundU account with the same email.
            var verified = string.Equals(principal.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email))
                return null;

            return new GoogleIdentity(subject, email, verified, principal.FindFirstValue("name"));
        }
        catch (SecurityTokenException)
        {
            // A bad token is an ordinary event, not an incident. Nothing from it is logged.
            return null;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning("Could not reach Google to verify a sign-in.");
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<IReadOnlyCollection<SecurityKey>?> GetSigningKeysAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyCollection<SecurityKey>? cached) && cached is { Count: > 0 })
            return cached;

        using var response = await _httpClient.GetAsync(_options.JwksUri, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var keys = new JsonWebKeySet(json).GetSigningKeys().ToList();
        if (keys.Count == 0) return null;

        _cache.Set(CacheKey, keys, TimeSpan.FromMinutes(Math.Clamp(_options.KeyCacheMinutes, 1, 24 * 60)));
        return keys;
    }
}
