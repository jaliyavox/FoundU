using FoundU.Application.Abstractions;
using FoundU.Application.Claims.Dtos;
using FoundU.Application.LostReports.Dtos;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Claims;
using FoundU.Infrastructure.Honor;
using FoundU.Infrastructure.Notifications;
using FoundU.Infrastructure.Persistence;

namespace FoundU.Tests;

/// <summary>
/// Seed data and fakes shared by every member's PostgreSQL tests (<c>Category=PostgreSql</c>).
/// Each helper writes rows with unique names, so the tests can share one test database.
/// </summary>
internal static class PostgresTestSupport
{
    public static ClaimService CreateClaimService(FoundUDbContext db)
        => new(db, new NotificationService(db), new NoopVerificationAgent(), new HonorService(db));

    public static async Task<(Claim Claim, LostReport Lost, FoundReport Found, AppUser Student, AppUser Staff)> SeedClaimAsync(FoundUDbContext db)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var student = NewUser($"student-{suffix}");
        var staff = NewUser($"staff-{suffix}", UserRole.Staff);
        var category = new Category { Name = $"Test category {suffix}" };
        var itemType = new ItemType { Name = $"Test item {suffix}", Category = category };
        var location = new CampusLocation { Name = $"Test location {suffix}" };
        var storage = new StorageLocation { Name = $"Test storage {suffix}" };
        var lost = new LostReport
        {
            Student = student, Category = category, ItemType = itemType, LastSeenLocation = location,
            Description = "Test backpack", EstimatedLostFromAt = DateTime.UtcNow.AddHours(-2),
            EstimatedLostToAt = DateTime.UtcNow.AddHours(-1), Status = LostReportStatus.Matched,
        };
        var found = new FoundReport
        {
            Staff = staff, Category = category, ItemType = itemType, FoundLocation = location,
            StorageLocation = storage, GeneralDescription = "Test backpack", FoundAt = DateTime.UtcNow,
            Status = FoundReportStatus.Unclaimed,
        };
        var claim = new Claim { Student = student, LostReport = lost, FoundReport = found, Status = ClaimStatus.UnderReview };
        db.Claims.Add(claim);
        await db.SaveChangesAsync();
        await AddCompletedVerificationAsync(db, claim);
        return (claim, lost, found, student, staff);
    }

    public static AppUser NewUser(string suffix, UserRole role = UserRole.Student)
        => new() { UserName = $"{suffix}@test.invalid", Email = $"{suffix}@test.invalid", FullName = "PostgreSQL Test User", Role = role };

    public static async Task<(Claim Claim, LostReport Lost)> AddCompetingClaimAsync(FoundUDbContext db, FoundReport found)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var student = NewUser($"competing-{suffix}");
        var lost = new LostReport
        {
            Student = student,
            CategoryId = found.CategoryId,
            ItemTypeId = found.ItemTypeId,
            LastSeenLocationId = found.FoundLocationId,
            Description = "Competing test backpack",
            EstimatedLostFromAt = DateTime.UtcNow.AddHours(-2),
            EstimatedLostToAt = DateTime.UtcNow.AddHours(-1),
            Status = LostReportStatus.Matched,
        };
        var claim = new Claim { Student = student, LostReport = lost, FoundReportId = found.Id, Status = ClaimStatus.UnderReview };
        db.Claims.Add(claim);
        await db.SaveChangesAsync();
        await AddCompletedVerificationAsync(db, claim);
        return (claim, lost);
    }

    public static async Task AddCompletedVerificationAsync(FoundUDbContext db, Claim claim)
    {
        var question = new VerificationQuestion { ClaimId = claim.Id, QuestionText = "What identifying detail does the item have?" };
        db.VerificationQuestions.Add(question);
        db.ClaimAnswers.Add(new ClaimAnswer
        {
            ClaimId = claim.Id,
            VerificationQuestion = question,
            AnswerText = "A distinctive mark",
            IsCorrect = true,
        });
        db.AgentRuns.Add(new AgentRun
        {
            ClaimId = claim.Id,
            TriggerEntityType = "Claim",
            TriggerEntityId = claim.Id,
            Objective = "Verification Agent evaluate_answers",
            Status = AgentRunStatus.Completed,
            FinalOutcomeJson = "{}",
            CompletedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    internal sealed class SuccessfulDescriptionParser : IDescriptionParserAgentClient
    {
        public Task<DescriptionParserAgentCallResult<DescriptionParserAgentResult>> ParseAsync(
            string description,
            string correlationId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(DescriptionParserAgentCallResult<DescriptionParserAgentResult>.Success(
                new("Backpack", "Blue", "Red", ["Red keychain"], .9m, "postgres-parser-run")));
    }

    internal sealed class NoopPhotoStorage : IPhotoStorage
    {
        public Task<string> SaveAsync(
            PhotoUpload upload,
            string folder,
            CancellationToken cancellationToken = default)
            => Task.FromResult("/unused");

        public Task DeleteAsync(string url, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    internal sealed class NoopVerificationAgent : IVerificationAgentClient
    {
        public Task<VerificationAgentCallResult<GenerateVerificationQuestionsResult>> GenerateQuestionsAsync(Guid claimId, IReadOnlyDictionary<string, string> details, string correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<VerificationAgentCallResult<EvaluateVerificationAnswersResult>> EvaluateAnswersAsync(Guid claimId, IReadOnlyList<VerificationAgentQuestion> questions, IReadOnlyDictionary<string, string> details, IReadOnlyList<VerificationAgentAnswer> answers, string correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
