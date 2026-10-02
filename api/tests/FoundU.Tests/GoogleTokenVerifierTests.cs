using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FoundU.Infrastructure.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FoundU.Tests;

/// <summary>
/// The real verifier against a real signed token, with Google's key set served locally.
/// Earlier tests used a fake verifier, which is how every real Google sign-in came to be
/// refused without a test noticing.
/// </summary>
public sealed class GoogleTokenVerifierTests
{
    private const string ClientId = "web-client.apps.googleusercontent.com";

    [Fact]
    public async Task AGenuineGoogleTokenIsAccepted()
    {
        using var rsa = RSA.Create(2048);
        var verifier = Verifier(rsa);

        var identity = await verifier.VerifyAsync(Token(rsa, ClientId));

        Assert.NotNull(identity);
        Assert.Equal("google-subject-123", identity!.Subject);
        Assert.Equal("student@gmail.com", identity.Email);
        Assert.True(identity.EmailVerified);
    }

    [Fact]
    public async Task AClientIdPastedWithStraySpaceStillMatches()
    {
        using var rsa = RSA.Create(2048);
        var verifier = Verifier(rsa, configuredClientId: $"  {ClientId}\n");

        // The button is handed the trimmed id, so that is the audience Google writes.
        Assert.Equal(ClientId, verifier.ClientId);
        Assert.NotNull(await verifier.VerifyAsync(Token(rsa, ClientId)));
    }

    [Fact]
    public async Task ATokenForAnotherAppIsRefused()
    {
        using var rsa = RSA.Create(2048);
        Assert.Null(await Verifier(rsa).VerifyAsync(Token(rsa, "someone-elses-app.apps.googleusercontent.com")));
    }

    [Fact]
    public async Task ATokenSignedWithAnotherKeyIsRefused()
    {
        using var google = RSA.Create(2048);
        using var forger = RSA.Create(2048);
        Assert.Null(await Verifier(google).VerifyAsync(Token(forger, ClientId)));
    }

    private static GoogleTokenVerifier Verifier(RSA rsa, string configuredClientId = ClientId)
    {
        var key = new RsaSecurityKey(rsa.ExportParameters(false)) { KeyId = "key-1" };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
        jwk.Use = "sig";
        jwk.Alg = "RS256";
        var json = $$"""{"keys":[{"kty":"RSA","kid":"key-1","use":"sig","alg":"RS256","n":"{{jwk.N}}","e":"{{jwk.E}}"}]}""";

        return new GoogleTokenVerifier(
            new HttpClient(new KeysHandler(json)),
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new GoogleAuthOptions { ClientId = configuredClientId, JwksUri = "https://keys.test/certs" }),
            NullLogger<GoogleTokenVerifier>.Instance);
    }

    private static string Token(RSA rsa, string audience)
    {
        var credentials = new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = "key-1" }, SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(
            issuer: "https://accounts.google.com",
            audience: audience,
            claims:
            [
                new Claim("sub", "google-subject-123"),
                new Claim("email", "student@gmail.com"),
                new Claim("email_verified", "true", ClaimValueTypes.Boolean),
                new Claim("name", "A Student"),
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class KeysHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
    }
}
