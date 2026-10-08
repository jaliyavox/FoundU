using System.Net;
using System.Net.Http.Json;

namespace FoundU.Tests;

/// <summary>
/// Defects found by the Assignment 2 security run (Newman SEC-27/28, OWASP ZAP): email-sending
/// endpoints had no rate limit, and API responses carried no hardening headers.
/// </summary>
[Trait("Member", "Member1-Jaliya")]
public sealed class SecurityHardeningTests : IClassFixture<FoundUWebApplicationFactory>
{
    private readonly FoundUWebApplicationFactory _factory;
    public SecurityHardeningTests(FoundUWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task ForgotPassword_IsLimitedToFivePerMinutePerAddress()
    {
        using var client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
            codes.Add((await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = $"limit-{i}@foundu.test" })).StatusCode);

        // Five accepted - the boundary - and the sixth refused.
        Assert.All(codes.Take(5), code => Assert.Equal(HttpStatusCode.Accepted, code));
        Assert.Equal(HttpStatusCode.TooManyRequests, codes[5]);

        var refused = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "again@foundu.test" });
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("60", refused.Headers.RetryAfter?.ToString());
        Assert.Contains("wait a minute", await refused.Content.ReadAsStringAsync());
    }

    // ZAP's active scan sent Search=%00: PostgreSQL refuses NUL in text, and it surfaced as a 500.
    [Theory]
    [InlineData("/api/lost-reports/feed?search=%00")]
    [InlineData("/api/found-posts/feed?search=a%00b")]
    public async Task ANullCharacterInTheQuery_IsABadRequest_NotAServerError(string url)
    {
        using var client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("null character", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ANullCharacterInAJsonBody_IsABadRequest_NotAServerError()
    {
        using var client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var response = await client.PostAsync("/api/auth/register", new StringContent(
            "{\"fullName\":\"Null \\u0000 Byte\",\"email\":\"nul@foundu.test\",\"password\":\"Password123\"}",
            System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ApiResponses_CarryHardeningHeaders()
    {
        using var client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync("/api/lost-reports/feed");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }
}
