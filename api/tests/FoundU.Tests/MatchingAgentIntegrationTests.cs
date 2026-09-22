using System.Text.Json;
using FoundU.Application.Abstractions;
using FoundU.Application.Matching.Dtos;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Matching;
using FoundU.Infrastructure.Notifications;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Tests;

public sealed class MatchingAgentIntegrationTests
{
    private const string Secret = "SECRET-OWNERSHIP-DETAIL-DO-NOT-LEAK";
    private const string StaffNote = "Staff observed matching straps.";

    [Fact]
    public async Task Candidate_UsesSafeServerContextAndCreatesOnlyASuggestion()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Agent.Result = MatchingAgentCallResult<MatchingAgentRecommendation>.Success(
            new("match_candidate", 1m, "remote-match-1"));

        var result = await fixture.Service.GenerateWithAgentAsync(
            new(fixture.Lost.Id, fixture.Found.Id, "  " + StaffNote + "  "), fixture.Staff.Id);

        Assert.Equal("match_candidate", result.Recommendation);
        Assert.Equal(1m, result.Score);
        Assert.NotNull(result.Suggestion);
        Assert.True(result.Suggestion!.IsAgentGenerated);
        Assert.Equal(1m, result.Suggestion.MatchScore);
        Assert.Equal(StaffNote, result.Suggestion.Note);
        Assert.Equal("Backpack", fixture.Agent.Lost!.ItemType);
        Assert.Equal("Blue", fixture.Agent.Found!.PrimaryColor);
        var sent = JsonSerializer.Serialize(new { fixture.Agent.Lost, fixture.Agent.Found });
        Assert.DoesNotContain(Secret, sent);
        Assert.DoesNotContain(StaffNote, sent);
        Assert.DoesNotContain("description", sent, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.Db.Claims);
        Assert.Equal(FoundReportStatus.Unclaimed, fixture.Found.Status);
        Assert.Equal(LostReportStatus.Active, fixture.Lost.Status);
        Assert.DoesNotContain(Secret, fixture.Db.AgentRuns.Single().FinalOutcomeJson!);
        Assert.DoesNotContain(StaffNote, fixture.Db.AgentRuns.Single().FinalOutcomeJson!);
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
    }

    [Fact]
    public async Task ManualReview_DoesNotCreateASuggestionOrStoreTheStaffNoteInAuditMetadata()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Agent.Result = MatchingAgentCallResult<MatchingAgentRecommendation>.Success(
            new("manual_review", 0m, "remote-match-3"));

        var result = await fixture.Service.GenerateWithAgentAsync(
            new(fixture.Lost.Id, fixture.Found.Id, StaffNote), fixture.Staff.Id);

        Assert.Equal("manual_review", result.Recommendation);
        Assert.Null(result.Suggestion);
        Assert.Empty(fixture.Db.MatchSuggestions);
        Assert.DoesNotContain(StaffNote, fixture.Db.AgentRuns.Single().FinalOutcomeJson!);
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
                StorageLocation = storage, GeneralDescription = "Blue backpack", PrimaryColor = "Blue", PrivateVerificationDetails = Secret, FoundAt = DateTime.UtcNow };
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
