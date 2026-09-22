using FoundU.Application.Abstractions;
using FoundU.Application.Claims.Dtos;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Claims;
using FoundU.Infrastructure.Notifications;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Tests;

public sealed class ClaimVerificationIntegrationTests
{
    [Fact]
    public async Task GenerateThenEvaluate_LikelyMatch_PersistsSafeQuestionsAndLeavesApprovalToStaff()
    {
        await using var fixture = await ClaimFixture.CreateAsync();
        fixture.Agent.GenerateResult = VerificationAgentCallResult<GenerateVerificationQuestionsResult>.Success(
            new(fixture.Claim.Id,
                [new("verification-1", "What identifying detail can you provide about the item?")],
                "manual_review",
                "remote-generation"));
        fixture.Agent.EvaluateResult = VerificationAgentCallResult<EvaluateVerificationAnswersResult>.Success(
            new(fixture.Claim.Id, "likely_match", "remote-evaluation"));

        var generated = await fixture.Service.GenerateQuestionsAsync(fixture.Claim.Id, fixture.Staff.Id);
        var question = generated.Questions.Single();

        Assert.Equal(nameof(ClaimStatus.WaitingForAnswer), generated.Status);
        Assert.DoesNotContain(fixture.Secret, System.Text.Json.JsonSerializer.Serialize(generated));
        Assert.Contains("verification-1", fixture.Db.AgentRuns.Single().FinalOutcomeJson!);

        var evaluated = await fixture.Service.SubmitAnswersAsync(
            fixture.Claim.Id,
            fixture.Student.Id,
            new SubmitClaimAnswersRequest([new(question.Id, "blue keychain")]));

        Assert.Equal(nameof(ClaimStatus.UnderReview), evaluated.Status);
        Assert.Empty(fixture.Db.ApprovalDecisions);
        Assert.Equal(FoundReportStatus.Unclaimed, fixture.FoundReport.Status);
        Assert.Equal(2, fixture.Db.AgentRuns.Count());
        Assert.DoesNotContain(fixture.Secret, string.Join(" ", fixture.Db.AgentRuns.Select(run => run.FinalOutcomeJson)));
    }

    [Fact]
    public async Task GenerateQuestions_AgentFailure_MovesClaimToManualReviewWithoutQuestions()
    {
        await using var fixture = await ClaimFixture.CreateAsync();
        fixture.Agent.GenerateResult = VerificationAgentCallResult<GenerateVerificationQuestionsResult>.Failure(
            "Verification agent timed out.");

        var result = await fixture.Service.GenerateQuestionsAsync(fixture.Claim.Id, fixture.Staff.Id);

        Assert.Equal(nameof(ClaimStatus.ManualReviewRequired), result.Status);
        Assert.Empty(result.Questions);
        Assert.Single(fixture.Db.AgentRuns);
        Assert.Equal(AgentRunStatus.Failed, fixture.Db.AgentRuns.Single().Status);
        Assert.DoesNotContain(fixture.Secret, fixture.Db.AgentRuns.Single().FinalOutcomeJson);
    }

    [Fact]
    public async Task CorruptDuplicatePersistedQuestionText_MovesClaimToManualReview()
    {
        await using var fixture = await ClaimFixture.CreateAsync();
        fixture.Agent.GenerateResult = VerificationAgentCallResult<GenerateVerificationQuestionsResult>.Success(
            new(fixture.Claim.Id,
                [
                    new("verification-1", "First safe question"),
                    new("verification-2", "Second safe question"),
                ],
                "manual_review",
                "remote-generation"));

        await fixture.Service.GenerateQuestionsAsync(fixture.Claim.Id, fixture.Staff.Id);
        var questions = fixture.Db.VerificationQuestions.OrderBy(question => question.Id).ToList();
        questions[1].QuestionText = questions[0].QuestionText;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.SubmitAnswersAsync(
            fixture.Claim.Id,
            fixture.Student.Id,
            new SubmitClaimAnswersRequest(questions.Select(question => new ClaimAnswerInput(question.Id, "answer")).ToList()));

        Assert.Equal(nameof(ClaimStatus.ManualReviewRequired), result.Status);
        Assert.Empty(fixture.Db.ApprovalDecisions);
    }

    private sealed class ClaimFixture : IAsyncDisposable
    {
        private ClaimFixture(FoundUDbContext db, ClaimService service, FakeVerificationAgentClient agent,
            Claim claim, AppUser student, AppUser staff, FoundReport foundReport, string secret)
        {
            Db = db;
            Service = service;
            Agent = agent;
            Claim = claim;
            Student = student;
            Staff = staff;
            FoundReport = foundReport;
            Secret = secret;
        }

        public FoundUDbContext Db { get; }
        public ClaimService Service { get; }
        public FakeVerificationAgentClient Agent { get; }
        public Claim Claim { get; }
        public AppUser Student { get; }
        public AppUser Staff { get; }
        public FoundReport FoundReport { get; }
        public string Secret { get; }

        public static async Task<ClaimFixture> CreateAsync()
        {
            var db = new FoundUDbContext(new DbContextOptionsBuilder<FoundUDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
            const string secret = "blue keychain in front pocket";
            var student = new AppUser { FullName = "Student", UserName = "student@test", Role = UserRole.Student };
            var staff = new AppUser { FullName = "Staff", UserName = "staff@test", Role = UserRole.Staff };
            var category = new Category { Name = "Bags" };
            var itemType = new ItemType { Name = "Backpack", Category = category };
            var location = new CampusLocation { Name = "Library" };
            var storage = new StorageLocation { Name = "Desk" };
            var lost = new LostReport
            {
                Student = student, Category = category, ItemType = itemType, LastSeenLocation = location,
                Description = "Blue backpack", EstimatedLostFromAt = DateTime.UtcNow.AddHours(-2),
                EstimatedLostToAt = DateTime.UtcNow.AddHours(-1),
            };
            var found = new FoundReport
            {
                Staff = staff, Category = category, ItemType = itemType, FoundLocation = location,
                StorageLocation = storage, GeneralDescription = "Backpack", PrivateVerificationDetails = secret,
                FoundAt = DateTime.UtcNow,
            };
            var claim = new Claim { Student = student, LostReport = lost, FoundReport = found, Status = ClaimStatus.Pending };
            db.Claims.Add(claim);
            await db.SaveChangesAsync();

            var agent = new FakeVerificationAgentClient();
            return new ClaimFixture(db, new ClaimService(db, new NotificationService(db), agent), agent,
                claim, student, staff, found, secret);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class FakeVerificationAgentClient : IVerificationAgentClient
    {
        public VerificationAgentCallResult<GenerateVerificationQuestionsResult> GenerateResult { get; set; }
            = VerificationAgentCallResult<GenerateVerificationQuestionsResult>.Failure("Not configured.");
        public VerificationAgentCallResult<EvaluateVerificationAnswersResult> EvaluateResult { get; set; }
            = VerificationAgentCallResult<EvaluateVerificationAnswersResult>.Failure("Not configured.");

        public Task<VerificationAgentCallResult<GenerateVerificationQuestionsResult>> GenerateQuestionsAsync(
            Guid claimId, IReadOnlyDictionary<string, string> privateVerificationDetails, string correlationId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(GenerateResult);

        public Task<VerificationAgentCallResult<EvaluateVerificationAnswersResult>> EvaluateAnswersAsync(
            Guid claimId, IReadOnlyList<VerificationAgentQuestion> questions,
            IReadOnlyDictionary<string, string> privateVerificationDetails,
            IReadOnlyList<VerificationAgentAnswer> answers, string correlationId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(EvaluateResult);
    }
}
