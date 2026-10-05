using FoundU.Application.Abstractions;
using FoundU.Application.Claims.Dtos;
using FoundU.Application.Claims.Validators;
using FoundU.Application.Common.Exceptions;
using FoundU.Application.LostReports.Dtos;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Administration;
using FoundU.Infrastructure.Claims;
using FoundU.Infrastructure.Honor;
using FoundU.Infrastructure.Notifications;
using FoundU.Infrastructure.Persistence;
using FoundU.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Tests;

/// <summary>
/// The rules that tie a lost report, its claims and the items on the shelf together: one lost
/// item is one found item, a closed report cannot be approved against, a report that is still
/// spoken for stays off the feed, and nothing leaves the desk without an ID check.
/// </summary>
public sealed class ClaimLifecycleTests
{
    [Fact]
    public async Task ApprovingOneItemClosesTheOwnersOtherClaimsAndNoNewOnesCanOpen()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = await fixture.Claims.CreateAsync(new(fixture.Report.Id, fixture.ItemA.Id), fixture.Owner.Id);
        var second = await fixture.Claims.CreateAsync(new(fixture.Report.Id, fixture.ItemB.Id), fixture.Owner.Id);

        await fixture.Claims.DecideAsync(first.Id, fixture.Staff.Id, new ClaimDecisionRequest("Approved", null));

        Assert.Equal(ClaimStatus.Cancelled, (await fixture.Db.Claims.SingleAsync(c => c.Id == second.Id)).Status);
        await Assert.ThrowsAsync<ConflictAppException>(() =>
            fixture.Claims.DecideAsync(second.Id, fixture.Staff.Id, new ClaimDecisionRequest("Approved", null)));
        await Assert.ThrowsAsync<ConflictAppException>(() =>
            fixture.Claims.CreateAsync(new(fixture.Report.Id, fixture.ItemC.Id), fixture.Owner.Id));
    }

    [Fact]
    public async Task WithdrawingCancelsOpenClaimsSoStaffCannotApproveAClosedReport()
    {
        await using var fixture = await Fixture.CreateAsync();
        var claim = await fixture.Claims.CreateAsync(new(fixture.Report.Id, fixture.ItemA.Id), fixture.Owner.Id);

        await fixture.Reports.WithdrawAsync(fixture.Report.Id, fixture.Owner.Id, "Found it at home");

        Assert.Equal(ClaimStatus.Cancelled, (await fixture.Db.Claims.SingleAsync()).Status);
        await Assert.ThrowsAsync<ConflictAppException>(() =>
            fixture.Claims.DecideAsync(claim.Id, fixture.Staff.Id, new ClaimDecisionRequest("Approved", null)));
        Assert.Equal(FoundReportStatus.Unclaimed, (await fixture.Db.FoundReports.SingleAsync(f => f.Id == fixture.ItemA.Id)).Status);
    }

    [Fact]
    public async Task AnOwnerCannotWithdrawWhileTheirItemWaitsAtTheDesk()
    {
        await using var fixture = await Fixture.CreateAsync();
        var claim = await fixture.Claims.CreateAsync(new(fixture.Report.Id, fixture.ItemA.Id), fixture.Owner.Id);
        await fixture.Claims.DecideAsync(claim.Id, fixture.Staff.Id, new ClaimDecisionRequest("Approved", null));

        await Assert.ThrowsAsync<ConflictAppException>(() =>
            fixture.Reports.WithdrawAsync(fixture.Report.Id, fixture.Owner.Id, null));
        Assert.Equal(LostReportStatus.Matched, (await fixture.Db.LostReports.SingleAsync()).Status);
    }

    [Fact]
    public async Task ARejectionDoesNotPutTheReportBackOnTheFeedWhileAFindersItemIsAtTheDesk()
    {
        await using var fixture = await Fixture.CreateAsync();
        var finder = new AppUser { FullName = "Finder", UserName = "finder@test", Role = UserRole.Student };
        fixture.Db.Users.Add(finder);
        fixture.Db.LostReportFoundClaims.Add(new LostReportFoundClaim
        {
            LostReportId = fixture.Report.Id,
            FinderId = finder.Id,
            Status = HandoverStatus.InCustody,
        });
        await fixture.Db.SaveChangesAsync();

        var claim = await fixture.Claims.CreateAsync(new(fixture.Report.Id, fixture.ItemA.Id), fixture.Owner.Id);
        await fixture.Claims.DecideAsync(claim.Id, fixture.Staff.Id, new ClaimDecisionRequest("Rejected", "Not a match."));

        Assert.Equal(LostReportStatus.Matched, (await fixture.Db.LostReports.SingleAsync()).Status);
    }

    [Fact]
    public async Task TheDeskMustCheckIdBeforeHandingOverAClaim()
    {
        var validator = new CollectClaimRequestValidator();
        Assert.False(validator.Validate(new CollectClaimRequest("123456")).IsValid);
        Assert.True(validator.Validate(new CollectClaimRequest("123456", OwnerIdChecked: true)).IsValid);

        await using var fixture = await Fixture.CreateAsync();
        var claim = await fixture.Claims.CreateAsync(new(fixture.Report.Id, fixture.ItemA.Id), fixture.Owner.Id);
        await fixture.Claims.DecideAsync(claim.Id, fixture.Staff.Id, new ClaimDecisionRequest("Approved", null));
        var code = (await fixture.Db.Claims.SingleAsync(c => c.Id == claim.Id)).CollectionCode!;

        var lookup = await fixture.Claims.GetByCollectionCodeAsync(code);
        Assert.Equal(claim.Id, lookup.Id);
        Assert.Null(lookup.CollectionCode);
    }

    [Fact]
    public async Task AnAdminMakesSomeoneStaffButNeverChangesTheirOwnRole()
    {
        await using var fixture = await Fixture.CreateAsync();
        var admin = new AppUser { FullName = "Admin", UserName = "admin@test", Email = "admin@test", Role = UserRole.Admin };
        fixture.Db.Users.Add(admin);
        fixture.Db.RefreshTokens.Add(new RefreshToken
        {
            UserId = fixture.Owner.Id,
            TokenHash = "hash",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
        });
        await fixture.Db.SaveChangesAsync();
        var users = new AdminUserService(fixture.Db);

        var promoted = await users.ChangeRoleAsync(fixture.Owner.Id, admin.Id, "Staff");

        Assert.Equal("Staff", promoted.Role);
        Assert.All(await fixture.Db.RefreshTokens.ToListAsync(), token => Assert.NotNull(token.RevokedAt));
        await Assert.ThrowsAsync<ValidationAppException>(() => users.ChangeRoleAsync(admin.Id, admin.Id, "Student"));
        await Assert.ThrowsAsync<ValidationAppException>(() => users.ChangeRoleAsync(fixture.Owner.Id, admin.Id, "7"));
        await Assert.ThrowsAsync<ConflictAppException>(() => users.ChangeRoleAsync(fixture.Owner.Id, admin.Id, "Staff"));
    }

    private sealed class NoAgent : IVerificationAgentClient
    {
        public Task<VerificationAgentCallResult<GenerateVerificationQuestionsResult>> GenerateQuestionsAsync(
            Guid claimId, IReadOnlyDictionary<string, string> privateVerificationDetails, string correlationId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(VerificationAgentCallResult<GenerateVerificationQuestionsResult>.Failure("not configured"));

        public Task<VerificationAgentCallResult<EvaluateVerificationAnswersResult>> EvaluateAnswersAsync(
            Guid claimId, IReadOnlyList<VerificationAgentQuestion> questions,
            IReadOnlyDictionary<string, string> privateVerificationDetails,
            IReadOnlyList<VerificationAgentAnswer> answers, string correlationId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(VerificationAgentCallResult<EvaluateVerificationAnswersResult>.Failure("not configured"));
    }

    private sealed class NoParser : IDescriptionParserAgentClient
    {
        public Task<DescriptionParserAgentCallResult<DescriptionParserAgentResult>> ParseAsync(
            string description, string correlationId, CancellationToken cancellationToken = default)
            => Task.FromResult(DescriptionParserAgentCallResult<DescriptionParserAgentResult>.Failure("not configured"));
    }

    private sealed class NoPhotos : IPhotoStorage
    {
        public Task<string> SaveAsync(PhotoUpload upload, string folder, CancellationToken cancellationToken = default) => Task.FromResult("/unused");
        public Task DeleteAsync(string url, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public required FoundUDbContext Db { get; init; }
        public required ClaimService Claims { get; init; }
        public required LostReportService Reports { get; init; }
        public required AppUser Owner { get; init; }
        public required AppUser Staff { get; init; }
        public required LostReport Report { get; init; }
        public required FoundReport ItemA { get; init; }
        public required FoundReport ItemB { get; init; }
        public required FoundReport ItemC { get; init; }

        public static async Task<Fixture> CreateAsync()
        {
            var db = new FoundUDbContext(new DbContextOptionsBuilder<FoundUDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

            var owner = new AppUser { FullName = "Owner Student", UserName = "owner@test", Email = "owner@test", Role = UserRole.Student };
            var staff = new AppUser { FullName = "Desk Staff", UserName = "staff@test", Email = "staff@test", Role = UserRole.Staff };
            var category = new Category { Name = "Bags" };
            var itemType = new ItemType { Name = "Backpack", Category = category };
            var location = new CampusLocation { Name = "Library" };
            var storage = new StorageLocation { Name = "Main desk" };
            var report = new LostReport
            {
                Student = owner,
                Category = category,
                ItemType = itemType,
                LastSeenLocation = location,
                Description = "Black backpack with two zips",
                EstimatedLostFromAt = DateTime.UtcNow.AddHours(-3),
                EstimatedLostToAt = DateTime.UtcNow.AddHours(-1),
                Status = LostReportStatus.Active,
            };

            FoundReport Item(string description) => new()
            {
                Staff = staff,
                Category = category,
                ItemType = itemType,
                FoundLocation = location,
                StorageLocation = storage,
                GeneralDescription = description,
                PrivateVerificationDetails = "a name tag inside",
                FoundAt = DateTime.UtcNow,
                Status = FoundReportStatus.Unclaimed,
            };
            var itemA = Item("Black backpack");
            var itemB = Item("Dark backpack");
            var itemC = Item("Another backpack");

            db.AddRange(owner, staff, category, itemType, location, storage, report, itemA, itemB, itemC);
            await db.SaveChangesAsync();

            var notifications = new NotificationService(db);
            var honor = new HonorService(db);
            return new Fixture
            {
                Db = db,
                Claims = new ClaimService(db, notifications, new NoAgent(), honor),
                Reports = new LostReportService(db, new NoPhotos(), notifications, new NoParser(), honor),
                Owner = owner,
                Staff = staff,
                Report = report,
                ItemA = itemA,
                ItemB = itemB,
                ItemC = itemC,
            };
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
