using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FoundU.Application.Abstractions;
using FoundU.Application.Claims.Dtos;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace FoundU.Tests;

[Collection(WebApplicationTestCollection.Name)]
public sealed class ClaimCreationEndpointTests : IClassFixture<FoundUWebApplicationFactory>
{
    private readonly FoundUWebApplicationFactory _factory;

    public ClaimCreationEndpointTests(FoundUWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_AuthenticatedStudent_CreatesPendingClaimForJwtUser()
    {
        var scenario = await SeedScenarioAsync();
        using var client = CreateAuthenticatedClient(scenario.AccessToken);

        var response = await client.PostAsJsonAsync(
            "/api/claims",
            new CreateClaimRequest(scenario.LostReportId, scenario.FoundReportId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ClaimResponse>();
        Assert.NotNull(body);
        Assert.Equal(scenario.LostReportId, body.LostReportId);
        Assert.Equal(scenario.FoundReportId, body.FoundReportId);
        Assert.Equal(nameof(ClaimStatus.Pending), body.Status);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FoundUDbContext>();
        var claim = await db.Claims.FindAsync(body.ClaimId);
        Assert.NotNull(claim);
        Assert.Equal(scenario.UserId, claim.StudentId);
        Assert.Equal(ClaimStatus.Pending, claim.Status);
    }

    [Fact]
    public async Task Create_WithoutAuthentication_Returns401()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/claims",
            new CreateClaimRequest(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_NonStudentRole_Returns403()
    {
        var scenario = await SeedScenarioAsync(role: UserRole.Staff);
        using var client = CreateAuthenticatedClient(scenario.AccessToken);

        var response = await PostClaimAsync(client, scenario);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_SuspendedStudent_Returns403()
    {
        var scenario = await SeedScenarioAsync(isSuspended: true);
        using var client = CreateAuthenticatedClient(scenario.AccessToken);

        var response = await PostClaimAsync(client, scenario);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithAnotherStudentsLostReport_Returns403()
    {
        var scenario = await SeedScenarioAsync(useDifferentLostReportOwner: true);
        using var client = CreateAuthenticatedClient(scenario.AccessToken);

        var response = await PostClaimAsync(client, scenario);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithMissingLostReport_Returns404()
    {
        var scenario = await SeedScenarioAsync();
        using var client = CreateAuthenticatedClient(scenario.AccessToken);

        var response = await client.PostAsJsonAsync(
            "/api/claims",
            new CreateClaimRequest(Guid.NewGuid(), scenario.FoundReportId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithMissingFoundReport_Returns404()
    {
        var scenario = await SeedScenarioAsync();
        using var client = CreateAuthenticatedClient(scenario.AccessToken);

        var response = await client.PostAsJsonAsync(
            "/api/claims",
            new CreateClaimRequest(scenario.LostReportId, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithResolvedLostReport_Returns400()
    {
        var scenario = await SeedScenarioAsync(lostReportStatus: LostReportStatus.Resolved);
        using var client = CreateAuthenticatedClient(scenario.AccessToken);

        var response = await PostClaimAsync(client, scenario);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithUnavailableFoundReport_Returns400()
    {
        var scenario = await SeedScenarioAsync(foundReportStatus: FoundReportStatus.Returned);
        using var client = CreateAuthenticatedClient(scenario.AccessToken);

        var response = await PostClaimAsync(client, scenario);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateActiveClaim_Returns409()
    {
        var scenario = await SeedScenarioAsync();
        await AddExistingClaimAsync(scenario, ClaimStatus.Pending);
        using var client = CreateAuthenticatedClient(scenario.AccessToken);

        var response = await PostClaimAsync(client, scenario);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_WhenFoundReportHasApprovedClaim_Returns409()
    {
        var scenario = await SeedScenarioAsync();
        await AddExistingClaimAsync(scenario, ClaimStatus.Approved, Guid.NewGuid());
        using var client = CreateAuthenticatedClient(scenario.AccessToken);

        var response = await PostClaimAsync(client, scenario);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_ResponseDoesNotExposePrivateVerificationFields()
    {
        var scenario = await SeedScenarioAsync();
        using var client = CreateAuthenticatedClient(scenario.AccessToken);

        var response = await PostClaimAsync(client, scenario);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var responseFields = document.RootElement
            .EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(
            new[] { "claimId", "lostReportId", "foundReportId", "status", "createdAt" }.Order(),
            responseFields.Order());
        Assert.DoesNotContain("privateVerificationDetails", responseFields);
        Assert.DoesNotContain("privateVerificationAttributesJson", responseFields);
    }

    private async Task<ClaimScenario> SeedScenarioAsync(
        UserRole role = UserRole.Student,
        bool isSuspended = false,
        bool useDifferentLostReportOwner = false,
        LostReportStatus lostReportStatus = LostReportStatus.Active,
        FoundReportStatus foundReportStatus = FoundReportStatus.Unclaimed)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FoundUDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var user = CreateUser(role, isSuspended);
        db.Users.Add(user);

        var lostReportOwner = user;
        if (useDifferentLostReportOwner)
        {
            lostReportOwner = CreateUser(UserRole.Student);
            db.Users.Add(lostReportOwner);
        }

        var lostReport = new LostReport
        {
            StudentId = lostReportOwner.Id,
            CategoryId = Guid.NewGuid(),
            ItemTypeId = Guid.NewGuid(),
            LastSeenLocationId = Guid.NewGuid(),
            Description = "Black backpack",
            EstimatedLostFromAt = DateTime.UtcNow.AddHours(-3),
            EstimatedLostToAt = DateTime.UtcNow.AddHours(-2),
            Status = lostReportStatus
        };

        var foundReport = new FoundReport
        {
            StaffId = Guid.NewGuid(),
            CategoryId = Guid.NewGuid(),
            ItemTypeId = Guid.NewGuid(),
            FoundLocationId = Guid.NewGuid(),
            StorageLocationId = Guid.NewGuid(),
            GeneralDescription = "Backpack received at the campus office",
            PrivateVerificationDetails = "Hidden zipper contains a pink keychain",
            PrivateVerificationAttributesJson = "{\"keychainColour\":\"pink\"}",
            FoundAt = DateTime.UtcNow.AddHours(-1),
            Status = foundReportStatus
        };

        db.LostReports.Add(lostReport);
        db.FoundReports.Add(foundReport);
        await db.SaveChangesAsync();

        return new ClaimScenario(
            user.Id,
            lostReport.Id,
            foundReport.Id,
            tokenService.GenerateAccessToken(user).Value);
    }

    private async Task AddExistingClaimAsync(
        ClaimScenario scenario,
        ClaimStatus status,
        Guid? studentId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FoundUDbContext>();
        db.Claims.Add(new Claim
        {
            StudentId = studentId ?? scenario.UserId,
            LostReportId = scenario.LostReportId,
            FoundReportId = scenario.FoundReportId,
            Status = status
        });
        await db.SaveChangesAsync();
    }

    private static AppUser CreateUser(UserRole role, bool isSuspended = false)
    {
        var id = Guid.NewGuid();
        var email = $"claim-{id:N}@foundu.test";
        return new AppUser
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FullName = "Claim Test User",
            Role = role,
            IsSuspended = isSuspended
        };
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

    private HttpClient CreateAuthenticatedClient(string accessToken)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static Task<HttpResponseMessage> PostClaimAsync(HttpClient client, ClaimScenario scenario) =>
        client.PostAsJsonAsync(
            "/api/claims",
            new CreateClaimRequest(scenario.LostReportId, scenario.FoundReportId));

    private sealed record ClaimScenario(
        Guid UserId,
        Guid LostReportId,
        Guid FoundReportId,
        string AccessToken);
}
