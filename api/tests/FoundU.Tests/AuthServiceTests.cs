using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using FoundU.Application.Abstractions;
using FoundU.Application.Auth;
using FoundU.Application.Auth.Dtos;
using FoundU.Application.Common.Exceptions;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure;
using FoundU.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FoundU.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task Register_ValidStudent_CreatesStudentAndReturnsTokens()
    {
        await using var app = await AuthTestApp.CreateAsync();
        var response = await app.Auth.RegisterAsync(new("Jane Student", "jane@foundu.test", "Password123", "STU-1"), "127.0.0.1");

        Assert.Equal(nameof(UserRole.Student), response.User.Role);
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
        Assert.Equal(UserRole.Student, (await app.Users.FindByEmailAsync("jane@foundu.test"))!.Role);
        Assert.Single(app.Db.RefreshTokens);
        Assert.DoesNotContain(response.RefreshToken, app.Db.RefreshTokens.Single().TokenHash);
    }

    [Fact]
    public async Task Register_DuplicateEmail_IsRejected()
    {
        await using var app = await AuthTestApp.CreateAsync();
        var request = new RegisterRequest("Jane", "duplicate@foundu.test", "Password123", null);
        await app.Auth.RegisterAsync(request, null);
        await Assert.ThrowsAsync<ConflictAppException>(() => app.Auth.RegisterAsync(request, null));
    }

    [Fact]
    public async Task Register_InvalidPassword_IsRejected()
    {
        await using var app = await AuthTestApp.CreateAsync();
        await Assert.ThrowsAsync<ValidationAppException>(() => app.Auth.RegisterAsync(
            new("Jane", "weak@foundu.test", "weak", null), null));
    }

    [Fact]
    public async Task Login_CorrectPassword_ReturnsTokens()
    {
        await using var app = await AuthTestApp.CreateAsync();
        await app.RegisterAsync("login@foundu.test");
        var response = await app.Auth.LoginAsync(new("login@foundu.test", "Password123"), null);
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
    }

    [Fact]
    public async Task Login_WrongPassword_IsRejected()
    {
        await using var app = await AuthTestApp.CreateAsync();
        await app.RegisterAsync("wrong-password@foundu.test");
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => app.Auth.LoginAsync(
            new("wrong-password@foundu.test", "WrongPassword123"), null));
    }

    [Fact]
    public async Task Login_FiveWrongPasswords_LocksAccount()
    {
        await using var app = await AuthTestApp.CreateAsync();
        await app.RegisterAsync("lockout@foundu.test");
        for (var attempt = 0; attempt < 5; attempt++)
            await Assert.ThrowsAsync<UnauthorizedAppException>(() => app.Auth.LoginAsync(new("lockout@foundu.test", "WrongPassword123"), null));

        var user = await app.Users.FindByEmailAsync("lockout@foundu.test");
        Assert.True(await app.Users.IsLockedOutAsync(user!));
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => app.Auth.LoginAsync(new("lockout@foundu.test", "Password123"), null));
    }

    [Fact]
    public async Task SuspendedUser_CannotLoginOrRefresh()
    {
        await using var app = await AuthTestApp.CreateAsync();
        var registration = await app.RegisterAsync("suspended@foundu.test");
        var user = await app.Users.FindByEmailAsync("suspended@foundu.test");
        user!.IsSuspended = true;
        await app.Users.UpdateAsync(user);

        await Assert.ThrowsAsync<ForbiddenAppException>(() => app.Auth.LoginAsync(new("suspended@foundu.test", "Password123"), null));
        await Assert.ThrowsAsync<ForbiddenAppException>(() => app.Auth.RefreshAsync(registration.RefreshToken, null));
    }

    [Fact]
    public async Task Refresh_RotatesTokenAndRevokesOriginal()
    {
        await using var app = await AuthTestApp.CreateAsync();
        var first = await app.RegisterAsync("rotate@foundu.test");
        var second = await app.Auth.RefreshAsync(first.RefreshToken, "127.0.0.2");

        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        var original = await app.Db.RefreshTokens.SingleAsync(t => t.TokenHash == app.Tokens.HashToken(first.RefreshToken));
        Assert.NotNull(original.RevokedAt);
        Assert.Equal(app.Tokens.HashToken(second.RefreshToken), original.ReplacedByTokenHash);
    }

    [Fact]
    public async Task Refresh_ReusedToken_RevokesReplacementChain()
    {
        await using var app = await AuthTestApp.CreateAsync();
        var first = await app.RegisterAsync("reuse@foundu.test");
        var second = await app.Auth.RefreshAsync(first.RefreshToken, null);
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => app.Auth.RefreshAsync(first.RefreshToken, null));

        var replacement = await app.Db.RefreshTokens.SingleAsync(t => t.TokenHash == app.Tokens.HashToken(second.RefreshToken));
        Assert.NotNull(replacement.RevokedAt);
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => app.Auth.RefreshAsync(second.RefreshToken, null));
    }

    [Fact]
    public async Task Refresh_InvalidOrExpiredToken_IsRejected()
    {
        await using var app = await AuthTestApp.CreateAsync();
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => app.Auth.RefreshAsync("not-a-token", null));
        var registration = await app.RegisterAsync("expired@foundu.test");
        var token = await app.Db.RefreshTokens.SingleAsync(t => t.TokenHash == app.Tokens.HashToken(registration.RefreshToken));
        token.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await app.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => app.Auth.RefreshAsync(registration.RefreshToken, null));
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        await using var app = await AuthTestApp.CreateAsync();
        var registration = await app.RegisterAsync("logout@foundu.test");
        await app.Auth.RevokeAsync(registration.RefreshToken, "127.0.0.3");

        var token = await app.Db.RefreshTokens.SingleAsync(t => t.TokenHash == app.Tokens.HashToken(registration.RefreshToken));
        Assert.NotNull(token.RevokedAt);
        Assert.Equal("Logged out", token.ReasonRevoked);
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => app.Auth.RefreshAsync(registration.RefreshToken, null));
    }

    [Fact]
    public async Task StudentPrincipal_DoesNotSatisfyStaffOrAdminPolicies()
    {
        await using var app = await AuthTestApp.CreateAsync();
        var registration = await app.RegisterAsync("policy@foundu.test");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(registration.AccessToken);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(jwt.Claims, "Bearer", ClaimTypes.Name, ClaimTypes.Role));
        var authorization = app.Services.GetRequiredService<IAuthorizationService>();

        Assert.False((await authorization.AuthorizeAsync(principal, null, PolicyNames.Staff)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(principal, null, PolicyNames.Admin)).Succeeded);
    }
}

public sealed class AuthEndpointTests : IClassFixture<FoundUWebApplicationFactory>
{
    private readonly FoundUWebApplicationFactory _factory;
    public AuthEndpointTests(FoundUWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Me_WithoutJwt_Returns401()
    {
        using var client = CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Me_WithValidJwt_ReturnsCurrentStudent()
    {
        using var client = CreateClient();
        var email = $"http-{Guid.NewGuid():N}@foundu.test";
        var registration = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("HTTP Student", email, "Password123", null));
        registration.EnsureSuccessStatusCode();
        var auth = await registration.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await client.GetAsync("/api/auth/me");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(email, body);
        Assert.Contains(nameof(UserRole.Student), body);
    }

    private HttpClient CreateClient() => _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
}

public sealed class FoundUWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        foreach (var setting in AuthTestApp.Settings)
            builder.UseSetting(setting.Key, setting.Value);
        builder.ConfigureServices(services => AuthTestApp.ReplaceDatabase(services, $"foundu-http-{Guid.NewGuid():N}"));
    }
}

internal sealed class AuthTestApp : IAsyncDisposable
{
    internal static readonly Dictionary<string, string?> Settings = new()
    {
        ["Jwt:Issuer"] = "FoundU.Tests",
        ["Jwt:Audience"] = "FoundU.Tests.Client",
        ["Jwt:SigningKey"] = "FoundU-tests-only-signing-key-at-least-32-bytes-long",
        ["Jwt:AccessTokenMinutes"] = "15",
        ["Jwt:RefreshTokenDays"] = "14",
        ["ConnectionStrings:FoundUDatabase"] = "Host=unused"
    };

    private readonly ServiceProvider _provider;
    private readonly AsyncServiceScope _scope;
    private AuthTestApp(ServiceProvider provider, AsyncServiceScope scope) { _provider = provider; _scope = scope; }
    internal IServiceProvider Services => _scope.ServiceProvider;
    internal IAuthService Auth => Services.GetRequiredService<IAuthService>();
    internal ITokenService Tokens => Services.GetRequiredService<ITokenService>();
    internal UserManager<AppUser> Users => Services.GetRequiredService<UserManager<AppUser>>();
    internal FoundUDbContext Db => Services.GetRequiredService<FoundUDbContext>();

    internal static async Task<AuthTestApp> CreateAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddFoundUInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(Settings).Build());
        ReplaceDatabase(services, $"foundu-auth-{Guid.NewGuid():N}");
        var provider = services.BuildServiceProvider();
        var scope = provider.CreateAsyncScope();
        var app = new AuthTestApp(provider, scope);
        await app.Db.Database.EnsureCreatedAsync();
        return app;
    }

    internal Task<AuthResponse> RegisterAsync(string email) => Auth.RegisterAsync(new("Test Student", email, "Password123", null), "127.0.0.1");

    internal static void ReplaceDatabase(IServiceCollection services, string databaseName)
    {
        services.RemoveAll<DbContextOptions<FoundUDbContext>>();
        services.RemoveAll<FoundUDbContext>();
        services.AddDbContext<FoundUDbContext>(options => options.UseInMemoryDatabase(databaseName));
    }

    public async ValueTask DisposeAsync() { await _scope.DisposeAsync(); await _provider.DisposeAsync(); }
}
