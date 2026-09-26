using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Application.Claims.Dtos;
using FoundU.Application.Abstractions;
using FoundU.Application.Notifications.Dtos;
using FoundU.Infrastructure.Claims;
using FoundU.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

namespace FoundU.Tests;

[Trait("Category", "PostgreSql")]
public sealed class PostgresPersistenceIntegrationTests
{
    [PostgresFact]
    public async Task MigrationsApplyAndModelHasNoPendingMigrations()
    {
        await using var db = PostgresTestDatabase.CreateContext();
        await PostgresTestDatabase.MigrateAsync(db);

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.True(await db.Database.CanConnectAsync());
    }

    [PostgresFact]
    public async Task AgentRunJsonbAndAgentStepForeignKeyRoundTrip()
    {
        await using var db = PostgresTestDatabase.CreateContext();
        await PostgresTestDatabase.MigrateAsync(db);
        var run = new AgentRun
        {
            TriggerEntityType = "PostgresIntegration",
            TriggerEntityId = Guid.NewGuid(),
            Objective = "PostgreSQL JSONB audit round-trip",
            PlanJson = "{\"steps\":[\"planning\"]}",
            FinalOutcomeJson = "{\"outcome\":\"safe\",\"retryCount\":2}",
            RetryCount = 2,
            Status = AgentRunStatus.Completed,
            CompletedAt = DateTime.UtcNow,
        };
        db.AgentRuns.Add(run);
        await db.SaveChangesAsync();
        db.AgentSteps.Add(new AgentStep
        {
            AgentRunId = run.Id,
            AgentName = AgentName.PlannerAgent,
            StepOrder = 1,
            Task = "Coordinator planning",
            Status = AgentStepStatus.Completed,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var reloaded = await db.AgentRuns.Include(item => item.Steps).SingleAsync(item => item.Id == run.Id);
        Assert.NotNull(reloaded.PlanJson);
        Assert.NotNull(reloaded.FinalOutcomeJson);
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("{\"steps\":[\"planning\"]}"),
            JsonNode.Parse(reloaded.PlanJson!)));
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("{\"outcome\":\"safe\",\"retryCount\":2}"),
            JsonNode.Parse(reloaded.FinalOutcomeJson!)));
        Assert.Equal(2, reloaded.RetryCount);
        Assert.Single(reloaded.Steps);
    }

    [PostgresFact]
    public async Task DeviceTokenUniqueIndexIsEnforcedByPostgreSql()
    {
        await using var db = PostgresTestDatabase.CreateContext();
        await PostgresTestDatabase.MigrateAsync(db);
        var suffix = Guid.NewGuid().ToString("N");
        var first = new AppUser { UserName = $"pg-{suffix}@test.invalid", Email = $"pg-{suffix}@test.invalid", FullName = "Postgres Test", Role = UserRole.Student };
        var second = new AppUser { UserName = $"pg2-{suffix}@test.invalid", Email = $"pg2-{suffix}@test.invalid", FullName = "Postgres Test Two", Role = UserRole.Student };
        db.Users.AddRange(first, second);
        await db.SaveChangesAsync();
        db.DeviceRegistrations.Add(new DeviceRegistration { UserId = first.Id, FcmToken = $"test-token-{suffix}", Platform = "android" });
        await db.SaveChangesAsync();
        db.DeviceRegistrations.Add(new DeviceRegistration { UserId = second.Id, FcmToken = $"test-token-{suffix}", Platform = "android" });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task ClaimApprovalCommitsAuthoritativeStateAndNotificationsTogether()
    {
        await using var db = PostgresTestDatabase.CreateContext();
        await PostgresTestDatabase.MigrateAsync(db);
        var seeded = await SeedClaimAsync(db);
        var service = CreateClaimService(db);

        await service.DecideAsync(seeded.Claim.Id, seeded.Staff.Id, new ClaimDecisionRequest("Approved", null));
        db.ChangeTracker.Clear();
        var claim = await db.Claims.SingleAsync(item => item.Id == seeded.Claim.Id);
        var found = await db.FoundReports.SingleAsync(item => item.Id == seeded.Found.Id);

        Assert.Equal(ClaimStatus.Approved, claim.Status);
        Assert.Equal(FoundReportStatus.Claimed, found.Status);
        Assert.False(string.IsNullOrWhiteSpace(claim.CollectionCode));
        Assert.Single(await db.ApprovalDecisions.Where(item => item.ClaimId == claim.Id && item.Decision == ApprovalDecisionType.Approved).ToListAsync());
        Assert.Equal(2, await db.Notifications.CountAsync(item => item.UserId == seeded.Student.Id && item.RelatedEntityId == claim.Id));
        await Assert.ThrowsAsync<FoundU.Application.Common.Exceptions.ConflictAppException>(() =>
            service.DecideAsync(claim.Id, seeded.Staff.Id, new ClaimDecisionRequest("Rejected", "second decision")));
        db.ChangeTracker.Clear();
        Assert.Equal(ClaimStatus.Approved, (await db.Claims.SingleAsync(item => item.Id == claim.Id)).Status);
    }

    [PostgresFact]
    public async Task ClaimRejectionPersistsDecisionAndReopensMatchedLostReport()
    {
        await using var db = PostgresTestDatabase.CreateContext();
        await PostgresTestDatabase.MigrateAsync(db);
        var seeded = await SeedClaimAsync(db);
        var service = CreateClaimService(db);

        await service.DecideAsync(seeded.Claim.Id, seeded.Staff.Id, new ClaimDecisionRequest("Rejected", "details do not match"));
        db.ChangeTracker.Clear();

        Assert.Equal(ClaimStatus.Rejected, (await db.Claims.SingleAsync(item => item.Id == seeded.Claim.Id)).Status);
        Assert.Equal(LostReportStatus.Active, (await db.LostReports.SingleAsync(item => item.Id == seeded.Lost.Id)).Status);
        Assert.Single(await db.ApprovalDecisions.Where(item => item.ClaimId == seeded.Claim.Id && item.Decision == ApprovalDecisionType.Rejected).ToListAsync());
        Assert.Single(await db.Notifications.Where(item => item.UserId == seeded.Student.Id && item.RelatedEntityId == seeded.Claim.Id).ToListAsync());
    }

    [PostgresFact]
    public async Task PartialUniqueIndexPreventsTwoApprovedClaimsForOneFoundReportAcrossContexts()
    {
        await using var firstContext = PostgresTestDatabase.CreateContext();
        await PostgresTestDatabase.MigrateAsync(firstContext);
        var seeded = await SeedClaimAsync(firstContext);
        seeded.Claim.Status = ClaimStatus.Approved;
        await firstContext.SaveChangesAsync();

        await using var secondContext = PostgresTestDatabase.CreateContext();
        var secondStudent = NewUser("second-owner");
        var secondLost = new LostReport
        {
            Student = secondStudent,
            CategoryId = seeded.Found.CategoryId,
            ItemTypeId = seeded.Found.ItemTypeId,
            LastSeenLocationId = seeded.Found.FoundLocationId,
            Description = "Another test backpack",
            EstimatedLostFromAt = DateTime.UtcNow.AddHours(-2),
            EstimatedLostToAt = DateTime.UtcNow.AddHours(-1),
            Status = LostReportStatus.Matched,
        };
        secondContext.Claims.Add(new Claim
        {
            Student = secondStudent,
            LostReport = secondLost,
            FoundReportId = seeded.Found.Id,
            Status = ClaimStatus.Approved,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => secondContext.SaveChangesAsync());
        await using var verifyContext = PostgresTestDatabase.CreateContext();
        Assert.Equal(1, await verifyContext.Claims.IgnoreQueryFilters().CountAsync(item => item.FoundReportId == seeded.Found.Id && item.Status == ClaimStatus.Approved));
    }

    [PostgresFact]
    public async Task ConflictingClaimApprovalRollsBackDecisionAndNotificationWrites()
    {
        await using var db = PostgresTestDatabase.CreateContext();
        await PostgresTestDatabase.MigrateAsync(db);
        var seeded = await SeedClaimAsync(db);
        seeded.Claim.Status = ClaimStatus.Approved;
        await db.SaveChangesAsync();
        var competing = await AddCompetingClaimAsync(db, seeded.Found);
        var service = CreateClaimService(db);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            service.DecideAsync(competing.Claim.Id, seeded.Staff.Id, new ClaimDecisionRequest("Approved", null)));
        db.ChangeTracker.Clear();

        Assert.Equal(ClaimStatus.UnderReview, (await db.Claims.SingleAsync(item => item.Id == competing.Claim.Id)).Status);
        Assert.Empty(await db.ApprovalDecisions.Where(item => item.ClaimId == competing.Claim.Id).ToListAsync());
        Assert.Empty(await db.Notifications.Where(item => item.RelatedEntityId == competing.Claim.Id).ToListAsync());
    }

    [PostgresFact]
    public async Task DeviceReregistrationMovesTokenAndUnregisterDeactivatesIt()
    {
        await using var db = PostgresTestDatabase.CreateContext();
        await PostgresTestDatabase.MigrateAsync(db);
        var first = NewUser("device-first");
        var second = NewUser("device-second");
        db.Users.AddRange(first, second);
        await db.SaveChangesAsync();
        var service = new DeviceRegistrationService(db);
        var token = $"test-device-token-{Guid.NewGuid():N}";

        await service.RegisterAsync(first.Id, new RegisterDeviceRequest(token, "android"));
        await service.RegisterAsync(second.Id, new RegisterDeviceRequest(token, "ios"));
        await service.UnregisterAsync(second.Id, new UnregisterDeviceRequest(token));
        db.ChangeTracker.Clear();

        var registration = await db.DeviceRegistrations.SingleAsync(item => item.FcmToken == token);
        Assert.Equal(second.Id, registration.UserId);
        Assert.False(registration.IsActive);
        Assert.NotNull(registration.DeactivatedAt);
    }

    private static ClaimService CreateClaimService(FoundU.Infrastructure.Persistence.FoundUDbContext db)
        => new(db, new NotificationService(db), new NoopVerificationAgent());

    private static async Task<(Claim Claim, LostReport Lost, FoundReport Found, AppUser Student, AppUser Staff)> SeedClaimAsync(FoundU.Infrastructure.Persistence.FoundUDbContext db)
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
        return (claim, lost, found, student, staff);
    }

    private static AppUser NewUser(string suffix, UserRole role = UserRole.Student)
        => new() { UserName = $"{suffix}@test.invalid", Email = $"{suffix}@test.invalid", FullName = "PostgreSQL Test User", Role = role };

    private static async Task<(Claim Claim, LostReport Lost)> AddCompetingClaimAsync(FoundU.Infrastructure.Persistence.FoundUDbContext db, FoundReport found)
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
        return (claim, lost);
    }

    private sealed class NoopVerificationAgent : IVerificationAgentClient
    {
        public Task<VerificationAgentCallResult<GenerateVerificationQuestionsResult>> GenerateQuestionsAsync(Guid claimId, IReadOnlyDictionary<string, string> details, string correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<VerificationAgentCallResult<EvaluateVerificationAnswersResult>> EvaluateAnswersAsync(Guid claimId, IReadOnlyList<VerificationAgentQuestion> questions, IReadOnlyDictionary<string, string> details, IReadOnlyList<VerificationAgentAnswer> answers, string correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
