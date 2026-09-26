using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FoundU.Application.Abstractions;
using FoundU.Application.Claims.Dtos;
using FoundU.Application.Common.Pagination;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FoundU.Tests;

/// <summary>
/// HTTP-level coverage of the student -> API -> verification-agent boundary.  The agent below
/// is deliberately controlled: CI never needs a FastAPI process, while the production client is
/// exercised separately by VerificationAgentClientTests.
/// </summary>
public sealed class ClaimsEndpointIntegrationTests
{
    private const string Secret = "SECRET-OWNERSHIP-DETAIL-DO-NOT-LEAK";

    [Fact]
    public async Task StudentToStaffApproval_UsesRecommendationOnlyAndNeverLeaksEvidence()
    {
        await using var app = await ClaimsHttpApp.CreateAsync("likely_match");
        using var student = app.ClientFor(app.Student);
        using var staff = app.ClientFor(app.Staff);

        var unauthenticated = await app.Factory.CreateClient().PostAsJsonAsync("/api/claims",
            new CreateClaimRequest(app.LostReport.Id, app.FoundReport.Id));
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        var created = await student.PostAsJsonAsync("/api/claims",
            new CreateClaimRequest(app.LostReport.Id, app.FoundReport.Id));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdBody = await created.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Secret, createdBody);
        var claim = JsonSerializer.Deserialize<ClaimDetailDto>(createdBody,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(claim);
        Assert.Equal(nameof(ClaimStatus.Pending), claim!.Status);
        Assert.Equal(app.Student.Id, claim.StudentId);
        Assert.Equal(LostReportStatus.Matched, await app.LostStatusAsync());

        using var otherStudent = app.ClientFor(app.OtherStudent);
        var forbiddenCreate = await otherStudent.PostAsJsonAsync("/api/claims",
            new CreateClaimRequest(app.LostReport.Id, app.FoundReport.Id));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCreate.StatusCode);

        var studentGenerate = await student.PostAsync($"/api/claims/{claim.Id}/questions/generate", null);
        Assert.Equal(HttpStatusCode.Forbidden, studentGenerate.StatusCode);

        var generated = await staff.PostAsync($"/api/claims/{claim.Id}/questions/generate", null);
        generated.EnsureSuccessStatusCode();
        var generatedBody = await generated.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Secret, generatedBody);
        var generatedClaim = JsonSerializer.Deserialize<ClaimDetailDto>(generatedBody,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(nameof(ClaimStatus.WaitingForAnswer), generatedClaim!.Status);
        AssertTrustedEvidence(app.Agent.LastGeneratedPrivateDetails);
        var question = generatedClaim.Questions.Single();

        var studentDetail = await student.GetAsync($"/api/claims/{claim.Id}");
        studentDetail.EnsureSuccessStatusCode();
        Assert.DoesNotContain(Secret, await studentDetail.Content.ReadAsStringAsync());

        var answered = await student.PostAsJsonAsync($"/api/claims/{claim.Id}/answers",
            new SubmitClaimAnswersRequest([new(question.Id, "blue keychain")]));
        answered.EnsureSuccessStatusCode();
        var answeredBody = await answered.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Secret, answeredBody);
        var evaluated = JsonSerializer.Deserialize<ClaimDetailDto>(answeredBody,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(nameof(ClaimStatus.UnderReview), evaluated!.Status);
        AssertTrustedEvidence(app.Agent.LastEvaluatedPrivateDetails);
        Assert.Null(evaluated.Decision);
        Assert.Equal(0, await app.DecisionCountAsync(claim.Id));
        Assert.Equal(FoundReportStatus.Unclaimed, await app.FoundStatusAsync());
        Assert.Equal(LostReportStatus.Matched, await app.LostStatusAsync());

        var approved = await staff.PostAsJsonAsync($"/api/claims/{claim.Id}/decision",
            new ClaimDecisionRequest("Approved", "Identity verified at the desk."));
        approved.EnsureSuccessStatusCode();
        var final = await approved.Content.ReadFromJsonAsync<ClaimDetailDto>();
        Assert.Equal(nameof(ClaimStatus.Approved), final!.Status);
        Assert.Equal(nameof(ApprovalDecisionType.Approved), final.Decision);
        Assert.Equal(1, await app.DecisionCountAsync(claim.Id));
        Assert.Equal(FoundReportStatus.Returned, await app.FoundStatusAsync());
        Assert.Equal(LostReportStatus.Resolved, await app.LostStatusAsync());
        Assert.True(await app.HasClaimHistoryAsync(claim.Id, ClaimStatus.Approved));
        Assert.True(await app.HasLostHistoryAsync(app.LostReport.Id, LostReportStatus.Resolved));
        Assert.DoesNotContain(Secret, await app.AgentAuditAsync(claim.Id));
    }

    [Fact]
    public async Task StudentMineEndpoints_ReturnOwnEmptyAndExistingData_WhileAdminIsForbidden()
    {
        await using var app = await ClaimsHttpApp.CreateAsync("likely_match");
        using var student = app.ClientFor(app.Student);
        using var admin = app.ClientFor(app.Admin);

        var emptyClaims = await student.GetAsync("/api/claims/mine?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, emptyClaims.StatusCode);
        var emptyPage = await emptyClaims.Content.ReadFromJsonAsync<PagedResult<ClaimListItemDto>>();
        Assert.NotNull(emptyPage);
        Assert.Empty(emptyPage!.Items);
        Assert.Equal(0, emptyPage.TotalCount);

        var adminClaims = await admin.GetAsync("/api/claims/mine?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.Forbidden, adminClaims.StatusCode);

        var ownReports = await student.GetAsync("/api/lost-reports/mine?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, ownReports.StatusCode);
        var adminReports = await admin.GetAsync("/api/lost-reports/mine?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.Forbidden, adminReports.StatusCode);

        var created = await student.PostAsJsonAsync("/api/claims",
            new CreateClaimRequest(app.LostReport.Id, app.FoundReport.Id));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var existingClaims = await student.GetAsync("/api/claims/mine?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, existingClaims.StatusCode);
        var existingPage = await existingClaims.Content.ReadFromJsonAsync<PagedResult<ClaimListItemDto>>();
        Assert.NotNull(existingPage);
        Assert.Single(existingPage!.Items);
        Assert.Equal(app.Student.Id, (await created.Content.ReadFromJsonAsync<ClaimDetailDto>())!.StudentId);
    }

    [Fact]
    public async Task StudentMine_ReturnsOkWhenTheVerificationServiceKeyIsMissing()
    {
        await using var factory = new MissingAiKeyWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FoundUDbContext>();
        var student = new AppUser
        {
            FullName = "No Key Student",
            UserName = "no-key.student@test",
            Email = "no-key.student@test",
            Role = UserRole.Student,
        };
        db.Users.Add(student);
        await db.SaveChangesAsync();

        using var client = factory.CreateClient();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.GenerateAccessToken(student).Value);

        var response = await client.GetAsync("/api/claims/mine?page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ClaimListItemDto>>();
        Assert.NotNull(page);
        Assert.Empty(page!.Items);
    }

    [Fact]
    public async Task DeviceRegistration_IsAuthenticatedAndOwnerScoped()
    {
        await using var app = await ClaimsHttpApp.CreateAsync("likely_match");
        const string token = "fcm-token-abcdefghijklmnopqrstuvwxyz-0123456789";
        using var student = app.ClientFor(app.Student);
        using var otherStudent = app.ClientFor(app.OtherStudent);

        var unauthenticated = await app.Factory.CreateClient().PostAsJsonAsync("/api/device-registrations",
            new { token, platform = "android" });
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        var registered = await student.PostAsJsonAsync("/api/device-registrations", new { token, platform = "android" });
        Assert.Equal(HttpStatusCode.NoContent, registered.StatusCode);
        var otherUnregister = await otherStudent.PostAsJsonAsync("/api/device-registrations/unregister", new { token });
        Assert.Equal(HttpStatusCode.NoContent, otherUnregister.StatusCode);

        await using var scope = app.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FoundUDbContext>();
        var registration = await db.DeviceRegistrations.SingleAsync();
        Assert.Equal(app.Student.Id, registration.UserId);
        Assert.True(registration.IsActive);
        Assert.DoesNotContain(token, await registered.Content.ReadAsStringAsync());
    }

    private static void AssertTrustedEvidence(IReadOnlyDictionary<string, string>? details)
    {
        Assert.NotNull(details);
        Assert.Single(details!);
        // BuildPrivateVerificationDetails maps the legacy free-text field to this trusted key.
        Assert.Equal(Secret, details["staff_verification_detail"]);
    }

    [Theory]
    [InlineData("manual_review")]
    [InlineData("unlikely_match")]
    public async Task NonLikelyRecommendation_RoutesOnlyToManualReview(string recommendation)
    {
        await using var app = await ClaimsHttpApp.CreateAsync(recommendation);
        var claim = await app.CreateAndGenerateAsync();
        using var student = app.ClientFor(app.Student);

        var answer = await student.PostAsJsonAsync($"/api/claims/{claim.Id}/answers",
            new SubmitClaimAnswersRequest([new(claim.Questions.Single().Id, "uncertain answer")]));
        answer.EnsureSuccessStatusCode();
        var detail = await answer.Content.ReadFromJsonAsync<ClaimDetailDto>();

        Assert.Equal(nameof(ClaimStatus.ManualReviewRequired), detail!.Status);
        Assert.Null(detail.Decision);
        Assert.Equal(0, await app.DecisionCountAsync(claim.Id));
        Assert.Equal(FoundReportStatus.Unclaimed, await app.FoundStatusAsync());
    }

    [Fact]
    public async Task StaffRejection_IsHumanDecisionAndReopensTheLostSearch()
    {
        await using var app = await ClaimsHttpApp.CreateAsync("manual_review");
        var claim = await app.CreateAndGenerateAsync();
        using var student = app.ClientFor(app.Student);
        using var staff = app.ClientFor(app.Staff);
        var answer = await student.PostAsJsonAsync($"/api/claims/{claim.Id}/answers",
            new SubmitClaimAnswersRequest([new(claim.Questions.Single().Id, "not enough detail")]));
        answer.EnsureSuccessStatusCode();

        var studentDecision = await student.PostAsJsonAsync($"/api/claims/{claim.Id}/decision",
            new ClaimDecisionRequest("Rejected", "No."));
        Assert.Equal(HttpStatusCode.Forbidden, studentDecision.StatusCode);

        var rejected = await staff.PostAsJsonAsync($"/api/claims/{claim.Id}/decision",
            new ClaimDecisionRequest("Rejected", "The evidence did not establish ownership."));
        rejected.EnsureSuccessStatusCode();
        var detail = await rejected.Content.ReadFromJsonAsync<ClaimDetailDto>();
        Assert.Equal(nameof(ClaimStatus.Rejected), detail!.Status);
        Assert.Equal(nameof(ApprovalDecisionType.Rejected), detail.Decision);
        Assert.Equal(1, await app.DecisionCountAsync(claim.Id));
        Assert.Equal(FoundReportStatus.Unclaimed, await app.FoundStatusAsync());
        Assert.Equal(LostReportStatus.Active, await app.LostStatusAsync());
    }

    [Fact]
    public async Task AgentFailure_IsSafeAndSecretNeverAppearsInStudentOrAuditData()
    {
        await using var app = await ClaimsHttpApp.CreateAsync("likely_match");
        app.Agent.FailGeneration = true;
        using var student = app.ClientFor(app.Student);
        using var staff = app.ClientFor(app.Staff);
        var created = await student.PostAsJsonAsync("/api/claims",
            new CreateClaimRequest(app.LostReport.Id, app.FoundReport.Id));
        var claim = await created.Content.ReadFromJsonAsync<ClaimDetailDto>();

        var generated = await staff.PostAsync($"/api/claims/{claim!.Id}/questions/generate", null);
        generated.EnsureSuccessStatusCode();
        var body = await generated.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Secret, body);
        var detail = JsonSerializer.Deserialize<ClaimDetailDto>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(nameof(ClaimStatus.ManualReviewRequired), detail!.Status);
        Assert.Empty(detail.Questions);
        Assert.Equal(FoundReportStatus.Unclaimed, await app.FoundStatusAsync());
        Assert.Equal(0, await app.DecisionCountAsync(claim.Id));
        Assert.DoesNotContain(Secret, await app.AgentAuditAsync(claim.Id));
    }

    [Fact]
    public async Task EvaluationFailure_AndAnotherStudentsAnswer_AreBothHandledSafely()
    {
        await using var app = await ClaimsHttpApp.CreateAsync("likely_match");
        var claim = await app.CreateAndGenerateAsync();
        using var otherStudent = app.ClientFor(app.OtherStudent);
        var forbidden = await otherStudent.PostAsJsonAsync($"/api/claims/{claim.Id}/answers",
            new SubmitClaimAnswersRequest([new(claim.Questions.Single().Id, "blue keychain")]));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        app.Agent.FailEvaluation = true;
        using var student = app.ClientFor(app.Student);
        var response = await student.PostAsJsonAsync($"/api/claims/{claim.Id}/answers",
            new SubmitClaimAnswersRequest([new(claim.Questions.Single().Id, "blue keychain")]));
        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<ClaimDetailDto>();
        Assert.Equal(nameof(ClaimStatus.ManualReviewRequired), detail!.Status);
        Assert.Equal(0, await app.DecisionCountAsync(claim.Id));
        Assert.Equal(FoundReportStatus.Unclaimed, await app.FoundStatusAsync());
        Assert.DoesNotContain(Secret, await app.AgentAuditAsync(claim.Id));
    }

    private sealed class ClaimsHttpApp : IAsyncDisposable
    {
        private ClaimsHttpApp(ClaimsWebApplicationFactory factory, TestVerificationAgent agent,
            AppUser student, AppUser otherStudent, AppUser staff, AppUser admin, LostReport lostReport, FoundReport foundReport)
            => (Factory, Agent, Student, OtherStudent, Staff, Admin, LostReport, FoundReport) =
                (factory, agent, student, otherStudent, staff, admin, lostReport, foundReport);

        public ClaimsWebApplicationFactory Factory { get; }
        public TestVerificationAgent Agent { get; }
        public AppUser Student { get; }
        public AppUser OtherStudent { get; }
        public AppUser Staff { get; }
        public AppUser Admin { get; }
        public LostReport LostReport { get; }
        public FoundReport FoundReport { get; }

        public static async Task<ClaimsHttpApp> CreateAsync(string recommendation)
        {
            var agent = new TestVerificationAgent { Recommendation = recommendation };
            var factory = new ClaimsWebApplicationFactory(agent);
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FoundUDbContext>();
            var student = new AppUser { FullName = "Claim Student", UserName = "claim.student@test", Email = "claim.student@test", Role = UserRole.Student };
            var other = new AppUser { FullName = "Other Student", UserName = "other.student@test", Email = "other.student@test", Role = UserRole.Student };
            var staff = new AppUser { FullName = "Claim Staff", UserName = "claim.staff@test", Email = "claim.staff@test", Role = UserRole.Staff };
            var admin = new AppUser { FullName = "Claim Admin", UserName = "claim.admin@test", Email = "claim.admin@test", Role = UserRole.Admin };
            var category = new Category { Name = "Bags" };
            var itemType = new ItemType { Name = "Backpack", Category = category };
            var location = new CampusLocation { Name = "Library" };
            var storage = new StorageLocation { Name = "Service Desk" };
            var lost = new LostReport { Student = student, Category = category, ItemType = itemType, LastSeenLocation = location,
                Description = "Blue backpack", EstimatedLostFromAt = DateTime.UtcNow.AddHours(-2), EstimatedLostToAt = DateTime.UtcNow.AddHours(-1) };
            var found = new FoundReport { Staff = staff, Category = category, ItemType = itemType, FoundLocation = location,
                StorageLocation = storage, GeneralDescription = "Blue backpack", PrivateVerificationDetails = Secret, FoundAt = DateTime.UtcNow };
            db.AddRange(student, other, staff, admin, lost, found);
            await db.SaveChangesAsync();
            return new ClaimsHttpApp(factory, agent, student, other, staff, admin, lost, found);
        }

        public HttpClient ClientFor(AppUser user)
        {
            var client = Factory.CreateClient();
            using var scope = Factory.Services.CreateScope();
            var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GenerateAccessToken(user).Value);
            return client;
        }

        public async Task<ClaimDetailDto> CreateAndGenerateAsync()
        {
            using var student = ClientFor(Student);
            using var staff = ClientFor(Staff);
            var created = await student.PostAsJsonAsync("/api/claims", new CreateClaimRequest(LostReport.Id, FoundReport.Id));
            created.EnsureSuccessStatusCode();
            var claim = (await created.Content.ReadFromJsonAsync<ClaimDetailDto>())!;
            var generated = await staff.PostAsync($"/api/claims/{claim.Id}/questions/generate", null);
            generated.EnsureSuccessStatusCode();
            return (await generated.Content.ReadFromJsonAsync<ClaimDetailDto>())!;
        }

        public Task<LostReportStatus> LostStatusAsync() => WithDb(db => db.LostReports.Where(x => x.Id == LostReport.Id).Select(x => x.Status).SingleAsync());
        public Task<FoundReportStatus> FoundStatusAsync() => WithDb(db => db.FoundReports.Where(x => x.Id == FoundReport.Id).Select(x => x.Status).SingleAsync());
        public Task<int> DecisionCountAsync(Guid claimId) => WithDb(db => db.ApprovalDecisions.CountAsync(x => x.ClaimId == claimId));
        public Task<bool> HasClaimHistoryAsync(Guid id, ClaimStatus status) => WithDb(db => db.ClaimStatusHistories.AnyAsync(x => x.ClaimId == id && x.ToStatus == status));
        public Task<bool> HasLostHistoryAsync(Guid id, LostReportStatus status) => WithDb(db => db.LostReportStatusHistories.AnyAsync(x => x.LostReportId == id && x.ToStatus == status));
        public Task<string> AgentAuditAsync(Guid id) => WithDb(async db => string.Join(" ", await db.AgentRuns.Where(x => x.ClaimId == id).Select(x => x.FinalOutcomeJson).ToListAsync()));
        private async Task<T> WithDb<T>(Func<FoundUDbContext, Task<T>> action) { await using var scope = Factory.Services.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<FoundUDbContext>()); }
        public async ValueTask DisposeAsync() => await Factory.DisposeAsync();
    }

    private sealed class ClaimsWebApplicationFactory(TestVerificationAgent agent) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            foreach (var setting in AuthTestApp.Settings) builder.UseSetting(setting.Key, setting.Value);
            builder.ConfigureServices(services =>
            {
                AuthTestApp.ReplaceDatabase(services, $"foundu-claims-http-{Guid.NewGuid():N}");
                services.RemoveAll<IVerificationAgentClient>();
                services.AddSingleton<IVerificationAgentClient>(agent);
            });
        }
    }

    private sealed class MissingAiKeyWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            foreach (var setting in AuthTestApp.Settings) builder.UseSetting(setting.Key, setting.Value);
            builder.UseSetting("AiService:ServiceKey", string.Empty);
            builder.ConfigureServices(services =>
                AuthTestApp.ReplaceDatabase(services, $"foundu-missing-ai-key-{Guid.NewGuid():N}"));
        }
    }

    private sealed class TestVerificationAgent : IVerificationAgentClient
    {
        public string Recommendation { get; set; } = "likely_match";
        public bool FailGeneration { get; set; }
        public bool FailEvaluation { get; set; }
        // Test-only in-memory capture; it is neither logged nor persisted by this double.
        public IReadOnlyDictionary<string, string>? LastGeneratedPrivateDetails { get; private set; }
        public IReadOnlyDictionary<string, string>? LastEvaluatedPrivateDetails { get; private set; }

        public Task<VerificationAgentCallResult<GenerateVerificationQuestionsResult>> GenerateQuestionsAsync(Guid claimId, IReadOnlyDictionary<string, string> privateVerificationDetails, string correlationId, CancellationToken cancellationToken = default)
        {
            LastGeneratedPrivateDetails = new Dictionary<string, string>(privateVerificationDetails);
            return Task.FromResult(FailGeneration
                ? VerificationAgentCallResult<GenerateVerificationQuestionsResult>.Failure("Verification agent is unavailable.")
                : VerificationAgentCallResult<GenerateVerificationQuestionsResult>.Success(new(claimId, [new("verification-1", "What identifying detail can you provide about the item?")], "manual_review", "test-generate")));
        }

        public Task<VerificationAgentCallResult<EvaluateVerificationAnswersResult>> EvaluateAnswersAsync(Guid claimId, IReadOnlyList<VerificationAgentQuestion> questions, IReadOnlyDictionary<string, string> privateVerificationDetails, IReadOnlyList<VerificationAgentAnswer> answers, string correlationId, CancellationToken cancellationToken = default)
        {
            LastEvaluatedPrivateDetails = new Dictionary<string, string>(privateVerificationDetails);
            return Task.FromResult(FailEvaluation
                ? VerificationAgentCallResult<EvaluateVerificationAnswersResult>.Failure("Verification agent timed out.")
                : VerificationAgentCallResult<EvaluateVerificationAnswersResult>.Success(new(claimId, Recommendation, "test-evaluate")));
        }
    }
}
