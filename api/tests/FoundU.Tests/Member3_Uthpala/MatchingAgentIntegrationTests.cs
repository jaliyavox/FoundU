using System.Text.Json;
using FoundU.Application.Abstractions;
using FoundU.Application.Common.Exceptions;
using FoundU.Application.Matching.Dtos;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Matching;
using FoundU.Infrastructure.Notifications;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Tests;

[Trait("Member", "Member3-Uthpala")]
public sealed class MatchingAgentIntegrationTests
{
    private const string Secret = "SECRET-OWNERSHIP-DETAIL-DO-NOT-LEAK";
    private const string StaffNote = "Staff observed matching straps.";

    [Theory]
    [InlineData(LostReportStatus.Withdrawn)]
    [InlineData(LostReportStatus.Resolved)]
    public async Task ClosedReportCannotReceiveManualOrAiSuggestion(LostReportStatus status)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Lost.Status = status;
        await fixture.Db.SaveChangesAsync();
        var request = new CreateMatchSuggestionRequest(fixture.Lost.Id, fixture.Found.Id, null);
        await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Service.CreateAsync(request, fixture.Staff.Id));
        await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Service.GenerateWithAgentAsync(request, fixture.Staff.Id));
        Assert.Empty(fixture.Db.MatchSuggestions);
        Assert.Null(fixture.Agent.Lost);
    }

    [Fact]
    public async Task ReportAwaitingApprovedCollectionCannotReceiveAnotherSuggestion()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Lost.Status = LostReportStatus.Matched;
        fixture.Db.Claims.Add(new Claim { LostReportId = fixture.Lost.Id, FoundReportId = fixture.Found.Id,
            StudentId = fixture.Lost.StudentId, Status = ClaimStatus.Approved });
        await fixture.Db.SaveChangesAsync();
        var request = new CreateMatchSuggestionRequest(fixture.Lost.Id, fixture.Found.Id, null);
        await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Service.CreateAsync(request, fixture.Staff.Id));
        await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Service.GenerateWithAgentAsync(request, fixture.Staff.Id));
        Assert.Empty(fixture.Db.MatchSuggestions);
        Assert.Null(fixture.Agent.Lost);
    }

    [Fact]
    public async Task Candidate_UsesSafeServerContextAndCreatesOnlyASuggestion()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Agent.Result = MatchingAgentCallResult<MatchingAgentRecommendation>.Success(
            new("match_candidate", 0.80m, "remote-match-1",
                ["Reported primary colour matched (20/20).", "Public identifying details matched (35/35).", "Reported times are in a strongly plausible sequence (25/25)."],
                ["Structured locations differ; no proximity metadata is available (0/20)."], []));

        var result = await fixture.Service.GenerateWithAgentAsync(
            new(fixture.Lost.Id, fixture.Found.Id, "  " + StaffNote + "  "), fixture.Staff.Id);

        Assert.Equal("match_candidate", result.Recommendation);
        Assert.Equal(0.80m, result.Score);
        Assert.NotNull(result.Suggestion);
        Assert.True(result.Suggestion!.IsAgentGenerated);
        Assert.Equal(0.80m, result.Suggestion.MatchScore);
        Assert.Contains("Public identifying details matched", result.Suggestion.MatchReason);
        Assert.Contains("Staff must verify ownership", result.Suggestion.MatchReason);
        Assert.Equal(StaffNote, result.Suggestion.Note);
        Assert.Equal("Backpack", fixture.Agent.Lost!.ItemType);
        Assert.Equal("Blue", fixture.Agent.Found!.PrimaryColor);
        var sent = JsonSerializer.Serialize(new { fixture.Agent.Lost, fixture.Agent.Found });
        Assert.DoesNotContain(Secret, sent);
        Assert.DoesNotContain(StaffNote, sent);
        Assert.Equal(fixture.Lost.Description, fixture.Agent.Lost.Description);
        Assert.Equal(fixture.Found.GeneralDescription, fixture.Agent.Found.Description);
        Assert.Equal(fixture.Lost.LastSeenLocationId.ToString(), fixture.Agent.Lost.Location);
        Assert.Equal(fixture.Found.FoundLocationId.ToString(), fixture.Agent.Found.Location);
        Assert.Equal(DateTimeKind.Utc, fixture.Agent.Lost.EventStartAt!.Value.Kind);
        Assert.Equal(fixture.Lost.EstimatedLostFromAt, fixture.Agent.Lost.EventStartAt);
        Assert.Equal(fixture.Found.FoundAt, fixture.Agent.Found.EventStartAt);
        Assert.DoesNotContain("PrivateVerification", sent, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.Db.Claims);
        Assert.Equal(FoundReportStatus.Unclaimed, fixture.Found.Status);
        Assert.Equal(LostReportStatus.Active, fixture.Lost.Status);
        Assert.DoesNotContain(Secret, fixture.Db.AgentRuns.Single().FinalOutcomeJson!);
        Assert.DoesNotContain(StaffNote, fixture.Db.AgentRuns.Single().FinalOutcomeJson!);
        Assert.DoesNotContain(Secret, result.Suggestion.MatchReason);
        Assert.Empty(await fixture.Service.GetReviewCandidatesForFoundReportAsync(fixture.Found.Id));
    }

    [Fact]
    public async Task Candidate_BlankStaffNote_IsStoredAsNullAndNeverSentToTheAgent()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Agent.Result = MatchingAgentCallResult<MatchingAgentRecommendation>.Success(
            new("match_candidate", 1m, "remote-match-blank-note"));

        var result = await fixture.Service.GenerateWithAgentAsync(
            new(fixture.Lost.Id, fixture.Found.Id, "   "), fixture.Staff.Id);

        Assert.NotNull(result.Suggestion);
        Assert.Null(result.Suggestion!.Note);
        Assert.DoesNotContain("StaffNote", JsonSerializer.Serialize(new { fixture.Agent.Lost, fixture.Agent.Found }));
    }

    [Fact]
    public async Task MissingColourStillReachesMatcherWithoutInflatingOrLeakingPrivateEvidence()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Lost.PrimaryColor = null;
        await fixture.Db.SaveChangesAsync();
        fixture.Agent.Result = MatchingAgentCallResult<MatchingAgentRecommendation>.Success(
            new("manual_review", 0.60m, "missing-colour", ["Public identifying details matched (35/35)."],
                ["Primary colour evidence is incomplete (0/20)."], []));

        var result = await fixture.Service.GenerateWithAgentAsync(
            new(fixture.Lost.Id, fixture.Found.Id, StaffNote), fixture.Staff.Id);

        Assert.Equal("manual_review", result.Recommendation);
        Assert.Null(fixture.Agent.Lost!.PrimaryColor);
        Assert.Null(result.Suggestion);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(fixture.Agent.Lost));
    }

    [Fact]
    public async Task FailedOrNonCandidateRecommendation_CreatesNoSuggestionAndChangesNoWorkflowStatus()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Agent.Result = MatchingAgentCallResult<MatchingAgentRecommendation>.Failure("network details must stay private");

        var result = await fixture.Service.GenerateWithAgentAsync(
            new(fixture.Lost.Id, fixture.Found.Id, StaffNote), fixture.Staff.Id);

        Assert.Equal("manual_review", result.Recommendation);
        Assert.Null(result.Suggestion);
        Assert.Empty(fixture.Db.MatchSuggestions);
        Assert.Empty(fixture.Db.Claims);
        Assert.Equal(FoundReportStatus.Unclaimed, fixture.Found.Status);
        Assert.Equal(LostReportStatus.Active, fixture.Lost.Status);
        var audit = fixture.Db.AgentRuns.Single();
        Assert.Equal(AgentRunStatus.Failed, audit.Status);
        Assert.DoesNotContain("network details", audit.ErrorMessage!);
        Assert.DoesNotContain(Secret, audit.FinalOutcomeJson!);
        Assert.DoesNotContain(StaffNote, audit.FinalOutcomeJson!);
    }

    [Fact]
    public async Task NoMatch_IsPersistedOnlyAsSafeAuditWithoutASuggestion()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Agent.Result = MatchingAgentCallResult<MatchingAgentRecommendation>.Success(
            new("no_match", 0m, "remote-match-2"));

        var result = await fixture.Service.GenerateWithAgentAsync(
            new(fixture.Lost.Id, fixture.Found.Id, StaffNote), fixture.Staff.Id);

        Assert.Equal("no_match", result.Recommendation);
        Assert.Null(result.Suggestion);
        Assert.Empty(fixture.Db.MatchSuggestions);
        Assert.DoesNotContain(StaffNote, fixture.Db.AgentRuns.Single().FinalOutcomeJson!);
        Assert.Empty(await fixture.Service.GetReviewCandidatesForFoundReportAsync(fixture.Found.Id));
    }

    [Fact]
    public async Task SilverBottleAt65Percent_IsDeduplicatedForStaffAndOnlyManualApprovalNotifiesStudent()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Lost.ItemType.Name = "Water Bottle";
        fixture.Lost.Description = "Silver stainless-steel water bottle near the library study area. Medium-sized and used regularly.";
        fixture.Found.GeneralDescription = "Silver metal water bottle beside tables in the library study area.";
        fixture.Lost.PrimaryColor = fixture.Found.PrimaryColor = "Silver";
        await fixture.Db.SaveChangesAsync();
        fixture.Agent.Result = MatchingAgentCallResult<MatchingAgentRecommendation>.Success(
            new("manual_review", 0.65m, "remote-match-3",
                ["Reported primary colour matched (20/20).", "Structured campus location matched (20/20).",
                    "Reported times are in a strongly plausible sequence (25/25)."],
                ["Public descriptions share no identifying detail (0/35)."], []));

        var pair = new CreateMatchSuggestionRequest(fixture.Lost.Id, fixture.Found.Id, StaffNote);
        var result = await fixture.Service.GenerateWithAgentAsync(pair, fixture.Staff.Id);
        await fixture.Service.GenerateWithAgentAsync(pair, fixture.Staff.Id);

        Assert.Equal("manual_review", result.Recommendation);
        Assert.Equal(0.65m, result.Score);
        Assert.Null(result.Suggestion);
        Assert.Empty(fixture.Db.MatchSuggestions);
        var review = Assert.Single(await fixture.Service.GetReviewCandidatesForFoundReportAsync(fixture.Found.Id));
        Assert.Equal(fixture.Lost.Id, review.LostReportId);
        Assert.Equal(0.65m, review.MatchScore);
        Assert.Contains("No shared identifying description detail", review.Explanation);
        Assert.Contains("Primary colour matches", review.Explanation);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(review));
        Assert.DoesNotContain(StaffNote, JsonSerializer.Serialize(review));
        Assert.All(fixture.Db.AgentRuns, audit => Assert.DoesNotContain(StaffNote, audit.FinalOutcomeJson!));
        Assert.Empty((await fixture.Service.GetForStudentAsync(fixture.Lost.StudentId, new())).Items);

        var suggestion = await fixture.Service.CreateAsync(pair, fixture.Staff.Id);
        Assert.False(suggestion.IsAgentGenerated);
        Assert.Null(suggestion.MatchScore);
        Assert.Single(fixture.Db.MatchSuggestions);
        Assert.Empty(await fixture.Service.GetReviewCandidatesForFoundReportAsync(fixture.Found.Id));
        Assert.Single((await fixture.Service.GetForStudentAsync(fixture.Lost.StudentId, new())).Items);
        await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Service.CreateAsync(pair, fixture.Staff.Id));
    }

    [Fact]
    public async Task GenericFortyPercentReviewDoesNotFillStaffQueue()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Agent.Result = MatchingAgentCallResult<MatchingAgentRecommendation>.Success(
            new("manual_review", 0.40m, "generic-review"));
        await fixture.Service.GenerateWithAgentAsync(
            new(fixture.Lost.Id, fixture.Found.Id, null), fixture.Staff.Id);
        Assert.Empty(await fixture.Service.GetReviewCandidatesForFoundReportAsync(fixture.Found.Id));
    }

    [Fact]
    public async Task LatestUnlikelyResultRemovesAnOlderReviewPair()
    {
        await using var fixture = await Fixture.CreateAsync();
        var pair = new CreateMatchSuggestionRequest(fixture.Lost.Id, fixture.Found.Id, null);
        fixture.Agent.Result = MatchingAgentCallResult<MatchingAgentRecommendation>.Success(
            new("manual_review", 0.65m, "older-review"));
        await fixture.Service.GenerateWithAgentAsync(pair, fixture.Staff.Id);
        Assert.Single(await fixture.Service.GetReviewCandidatesForFoundReportAsync(fixture.Found.Id));
        fixture.Agent.Result = MatchingAgentCallResult<MatchingAgentRecommendation>.Success(
            new("no_match", 0.25m, "newer-unlikely"));
        await fixture.Service.GenerateWithAgentAsync(pair, fixture.Staff.Id);
        Assert.Empty(await fixture.Service.GetReviewCandidatesForFoundReportAsync(fixture.Found.Id));
    }

    [Fact]
    public async Task ReviewQueueExcludesClosedReportsAndItems()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Agent.Result = MatchingAgentCallResult<MatchingAgentRecommendation>.Success(
            new("manual_review", 0.65m, "status-review"));
        await fixture.Service.GenerateWithAgentAsync(
            new(fixture.Lost.Id, fixture.Found.Id, null), fixture.Staff.Id);
        fixture.Lost.Status = LostReportStatus.Withdrawn;
        await fixture.Db.SaveChangesAsync();
        Assert.Empty(await fixture.Service.GetReviewCandidatesForFoundReportAsync(fixture.Found.Id));
        fixture.Lost.Status = LostReportStatus.Active;
        fixture.Found.Status = FoundReportStatus.Returned;
        await fixture.Db.SaveChangesAsync();
        Assert.Empty(await fixture.Service.GetReviewCandidatesForFoundReportAsync(fixture.Found.Id));
    }

    [Fact]
    public void MatchingGeneration_RemainsStaffAuthorized()
    {
        var controller = typeof(FoundU.Api.Controllers.MatchSuggestionsController);
        Assert.NotEmpty(controller.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true));
        var method = controller.GetMethod("GenerateWithAgent")!;
        var authorization = Assert.Single(method.GetCustomAttributes(
            typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true));
        Assert.Equal(FoundU.Application.Auth.PolicyNames.Staff,
            ((Microsoft.AspNetCore.Authorization.AuthorizeAttribute)authorization).Policy);
        var reviewMethod = controller.GetMethod("ReviewForItem")!;
        var reviewAuth = Assert.Single(reviewMethod.GetCustomAttributes(
            typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true));
        Assert.Equal(FoundU.Application.Auth.PolicyNames.Staff,
            ((Microsoft.AspNetCore.Authorization.AuthorizeAttribute)reviewAuth).Policy);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(FoundUDbContext db, MatchSuggestionService service, FakeMatchingAgent agent,
            AppUser staff, LostReport lost, FoundReport found)
            => (Db, Service, Agent, Staff, Lost, Found) = (db, service, agent, staff, lost, found);

        public FoundUDbContext Db { get; }
        public MatchSuggestionService Service { get; }
        public FakeMatchingAgent Agent { get; }
        public AppUser Staff { get; }
        public LostReport Lost { get; }
        public FoundReport Found { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var db = new FoundUDbContext(new DbContextOptionsBuilder<FoundUDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var student = new AppUser { FullName = "Student", UserName = "student@test", Role = UserRole.Student };
            var staff = new AppUser { FullName = "Staff", UserName = "staff@test", Role = UserRole.Staff };
            var category = new Category { Name = "Bags" };
            var type = new ItemType { Name = "Backpack", Category = category };
            var location = new CampusLocation { Name = "Library" };
            var storage = new StorageLocation { Name = "Desk" };
            var lost = new LostReport { Student = student, Category = category, ItemType = type, LastSeenLocation = location,
                Description = "Blue backpack", PrimaryColor = "Blue", EstimatedLostFromAt = DateTime.UtcNow.AddHours(-2), EstimatedLostToAt = DateTime.UtcNow };
            var found = new FoundReport { Staff = staff, Category = category, ItemType = type, FoundLocation = location,
                StorageLocation = storage, GeneralDescription = "Blue backpack", PrimaryColor = "Blue", PrivateVerificationDetails = Secret, PrivateVerificationAttributesJson = "{\"evidence\":\"" + Secret + "\"}", ObservedAttributesJson = "{\"raw\":\"" + Secret + "\"}", FoundAt = DateTime.UtcNow };
            db.AddRange(student, staff, lost, found);
            await db.SaveChangesAsync();
            var agent = new FakeMatchingAgent();
            return new Fixture(db, new MatchSuggestionService(db, new NotificationService(db), agent), agent, staff, lost, found);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class FakeMatchingAgent : IMatchingAgentClient
    {
        public MatchingAgentReportSummary? Lost { get; private set; }
        public MatchingAgentReportSummary? Found { get; private set; }
        public MatchingAgentCallResult<MatchingAgentRecommendation> Result { get; set; }
            = MatchingAgentCallResult<MatchingAgentRecommendation>.Failure("Not configured.");

        public Task<MatchingAgentCallResult<MatchingAgentRecommendation>> MatchReportsAsync(
            MatchingAgentReportSummary lostReport, MatchingAgentReportSummary foundReport, string correlationId,
            CancellationToken cancellationToken = default)
        {
            Lost = lostReport;
            Found = foundReport;
            return Task.FromResult(Result);
        }
    }
}
