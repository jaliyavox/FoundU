using FoundU.Application.Claims.Dtos;
using FoundU.Application.Common.Exceptions;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Claims;
using Microsoft.EntityFrameworkCore;
using static FoundU.Tests.PostgresTestSupport;

namespace FoundU.Tests;

/// <summary>Claim decisions, their uniqueness rules and concurrent approvals, on real PostgreSQL.</summary>
[Trait("Category", "PostgreSql")]
[Trait("Member", "Member4-Braveena")]
public sealed class Member4DatabaseTests
{
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
        await Assert.ThrowsAsync<ConflictAppException>(() =>
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
    public async Task ActiveClaimPairIsUniqueAcrossDatabaseContexts()
    {
        await using var first = PostgresTestDatabase.CreateContext();
        await PostgresTestDatabase.MigrateAsync(first);
        var seeded = await SeedClaimAsync(first);
        await using var second = PostgresTestDatabase.CreateContext();
        second.Claims.Add(new Claim
        {
            StudentId = seeded.Claim.StudentId,
            LostReportId = seeded.Claim.LostReportId,
            FoundReportId = seeded.Found.Id,
            Status = ClaimStatus.Pending,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
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
    public async Task TwoStaffApprovingRivalClaimsAtOnceLeavesOneWinnerAndOneStory()
    {
        await using var setup = PostgresTestDatabase.CreateContext();
        await PostgresTestDatabase.MigrateAsync(setup);
        var seeded = await SeedClaimAsync(setup);
        var rival = await AddCompetingClaimAsync(setup, seeded.Found);

        // Two desks, two connections, the same moment. Before the row version, each approval
        // rejected the other's claim and both saves landed: both students were told approved
        // and rejected, and the loser kept a collection code.
        await using var deskA = PostgresTestDatabase.CreateContext();
        await using var deskB = PostgresTestDatabase.CreateContext();
        var results = await Task.WhenAll(
            TryApprove(CreateClaimService(deskA), seeded.Claim.Id, seeded.Staff.Id),
            TryApprove(CreateClaimService(deskB), rival.Claim.Id, seeded.Staff.Id));

        Assert.Equal(1, results.Count(ok => ok));
        await using var verify = PostgresTestDatabase.CreateContext();
        var claims = await verify.Claims.IgnoreQueryFilters()
            .Where(c => c.FoundReportId == seeded.Found.Id).ToListAsync();
        var winner = Assert.Single(claims, c => c.Status == ClaimStatus.Approved);
        Assert.All(claims.Where(c => c.Id != winner.Id), loser => Assert.Null(loser.CollectionCode));
        Assert.Equal(1, await verify.Notifications.CountAsync(
            n => n.Type == NotificationType.ClaimApproved && (n.RelatedEntityId == seeded.Claim.Id || n.RelatedEntityId == rival.Claim.Id)));

        static async Task<bool> TryApprove(ClaimService service, Guid claimId, Guid staffId)
        {
            try
            {
                await service.DecideAsync(claimId, staffId, new ClaimDecisionRequest("Approved", null));
                return true;
            }
            catch (Exception error) when (error is ConflictAppException or DbUpdateException)
            {
                return false;
            }
        }
    }
}
