using FoundU.Application.Abstractions;
using FoundU.Application.Common.Exceptions;
using FoundU.Application.Handovers.Dtos;
using FoundU.Application.LostReports.Dtos;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Handovers;
using FoundU.Infrastructure.Honor;
using FoundU.Infrastructure.Notifications;
using FoundU.Infrastructure.Persistence;
using FoundU.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Tests;

/// <summary>
/// A finder walking somebody's item to a desk, and the owner collecting it there.
///
/// The rules that matter: one code, known only to those two; the notice comes off the feed
/// while the walk is on and comes back by itself if nobody turns up; the desk cannot release
/// the item without saying it checked who the collector is; and a code works once.
/// </summary>
[Trait("Member", "Member2-Ranasinghe")]
public sealed class HandoverTests
{
    // The owner's tracker sat on "Reported" while a finder handed the item in: progress was
    // read only from suggestions and claims. Each step of the finder's route now moves it.
    [Fact]
    public async Task TheOwnersProgressFollowsTheFinderAllTheWayToTheDesk()
    {
        await using var fixture = await Fixture.CreateAsync();
        async Task<string> StageAsync() =>
            (await fixture.Reports.SearchForStudentAsync(fixture.Owner.Id, new LostReportQuery())).Items.Single().ProgressStage;

        Assert.Equal("Reported", await StageAsync());

        await fixture.Reports.RegisterFoundClaimAsync(fixture.Report.Id, fixture.Finder.Id);
        Assert.Equal("FinderFound", await StageAsync());

        var started = await fixture.Handovers.StartAsync(fixture.Report.Id, fixture.Finder.Id);
        Assert.Equal("FinderOnTheWay", await StageAsync());

        await fixture.Handovers.ReceiveAsync(started.Code!, fixture.Staff.Id, new ReceiveHandoverRequest(fixture.Storage.Id, null));
        Assert.Equal("AtDesk", await StageAsync());
        Assert.Equal("AtDesk", (await fixture.Reports.GetByIdAsync(fixture.Report.Id, fixture.Owner.Id, false)).ProgressStage);
    }

    [Fact]
    public async Task WithdrawingTheReportStopsTheFindersCodeWorkingAtTheDesk()
    {
        await using var fixture = await Fixture.CreateAsync();
        var started = await fixture.Handovers.StartAsync(fixture.Report.Id, fixture.Finder.Id);

        await fixture.Reports.WithdrawAsync(fixture.Report.Id, fixture.Owner.Id, "Found it at home");

        var handover = await fixture.Db.LostReportFoundClaims.SingleAsync();
        Assert.Equal(HandoverStatus.Cancelled, handover.Status);
        Assert.Null(handover.HandoverCode);
        await Assert.ThrowsAsync<NotFoundAppException>(() => fixture.Handovers.ReceiveAsync(
            started.Code!, fixture.Staff.Id, new ReceiveHandoverRequest(fixture.Storage.Id, null)));

        // The report stays withdrawn, and no orphaned found item was logged against it.
        Assert.Equal(LostReportStatus.Withdrawn, (await fixture.Db.LostReports.SingleAsync()).Status);
        Assert.False(await fixture.Db.FoundReports.AnyAsync());
    }

    [Fact]
    public async Task StartingAHandoverMintsOneCodeAndTakesTheNoticeOffTheFeed()
    {
        await using var fixture = await Fixture.CreateAsync();

        var handover = await fixture.Handovers.StartAsync(fixture.Report.Id, fixture.Finder.Id);

        Assert.Equal("AwaitingHandIn", handover.Status);
        Assert.NotNull(handover.Code);
        Assert.Equal(6, handover.Code!.Length);
        Assert.All(handover.Code, character => Assert.True(char.IsDigit(character)));

        var report = await fixture.Db.LostReports.SingleAsync();
        Assert.NotNull(report.PausedUntil);
        Assert.True(report.PausedUntil > DateTime.UtcNow);

        // Off the public feed while it is on its way.
        var feed = await fixture.Reports.GetPublicFeedAsync(new LostReportQuery());
        Assert.Empty(feed.Items);

        // Pressing it again is the same walk, not a second code.
        var again = await fixture.Handovers.StartAsync(fixture.Report.Id, fixture.Finder.Id);
        Assert.Equal(handover.Code, again.Code);
        Assert.Equal(1, await fixture.Db.LostReportFoundClaims.CountAsync());
    }

    [Fact]
    public async Task OnlyTheFinderAndTheOwnerEverSeeTheCode()
    {
        await using var fixture = await Fixture.CreateAsync();
        var started = await fixture.Handovers.StartAsync(fixture.Report.Id, fixture.Finder.Id);

        var ownerView = await fixture.Handovers.GetForUserAsync(fixture.Report.Id, fixture.Owner.Id);
        Assert.Equal(started.Code, ownerView!.Code);

        // A third student has no handover of their own here, so there is nothing to read.
        Assert.Null(await fixture.Handovers.GetForUserAsync(fixture.Report.Id, fixture.Other.Id));

        // And the desk's own lookup never carries the code back out.
        var lookup = await fixture.Handovers.LookupAsync(started.Code!);
        Assert.DoesNotContain(started.Code!, System.Text.Json.JsonSerializer.Serialize(lookup));
        Assert.Equal("receive", lookup.NextStep);
    }

    [Fact]
    public async Task TheOwnerCannotHandTheirOwnItemIn()
    {
        await using var fixture = await Fixture.CreateAsync();

        await Assert.ThrowsAsync<ForbiddenAppException>(
            () => fixture.Handovers.StartAsync(fixture.Report.Id, fixture.Owner.Id));
    }

    [Fact]
    public async Task TwoPeopleCannotBeCarryingTheSameItem()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Handovers.StartAsync(fixture.Report.Id, fixture.Finder.Id);

        await Assert.ThrowsAsync<ConflictAppException>(
            () => fixture.Handovers.StartAsync(fixture.Report.Id, fixture.Other.Id));
    }

    [Fact]
    public async Task TheDeskTakesTheItemAndTheOwnerIsToldWhereItIs()
    {
        await using var fixture = await Fixture.CreateAsync();
        var started = await fixture.Handovers.StartAsync(fixture.Report.Id, fixture.Finder.Id);

        var received = await fixture.Handovers.ReceiveAsync(
            started.Code!, fixture.Staff.Id, new ReceiveHandoverRequest(fixture.Storage.Id, null));

        Assert.Equal("InCustody", received.Status);
        Assert.Equal("release", received.NextStep);
        Assert.Equal("Main Desk", received.StorageLocationName);
        // Receiving a finder's item sets the legacy report status to Matched and creates no
        // ownership claim - but the owner's tracker shows it waiting at the desk.
        Assert.Equal(LostReportStatus.Matched, (await fixture.Db.LostReports.SingleAsync()).Status);
        Assert.Empty(await fixture.Db.Claims.ToListAsync());
        Assert.Equal("AtDesk", (await fixture.Reports.GetByIdAsync(fixture.Report.Id, fixture.Owner.Id, false)).ProgressStage);

        // The item is now logged like anything else in custody, and the two records point at
        // each other.
        var item = await fixture.Db.FoundReports.SingleAsync();
        Assert.Equal(fixture.Storage.Id, item.StorageLocationId);
        Assert.Equal(fixture.Finder.Id, item.FinderId);
        var claim = await fixture.Db.LostReportFoundClaims.SingleAsync();
        Assert.Equal(item.Id, claim.FoundReportId);

        // The clock stops - it is on a shelf now, not in a bag.
        Assert.Null(claim.HandoverExpiresAt);
        Assert.Null((await fixture.Db.LostReports.SingleAsync()).PausedUntil);

        Assert.Contains(
            await fixture.Db.Notifications.Where(n => n.UserId == fixture.Owner.Id).ToListAsync(),
            n => n.Message.Contains("Main Desk"));

        // Handing in earns the finder credit; taking it home is a separate award.
        var award = await fixture.Db.HonorAwards.SingleAsync();
        Assert.Equal(HonorAwardReason.HandedInAtDesk, award.Reason);
    }

    [Fact]
    public async Task TheDeskCannotReleaseAnItemWithoutCheckingWhoIsCollectingIt()
    {
        await using var fixture = await Fixture.CreateAsync();
        var started = await fixture.Handovers.StartAsync(fixture.Report.Id, fixture.Finder.Id);
        await fixture.Handovers.ReceiveAsync(started.Code!, fixture.Staff.Id, new ReceiveHandoverRequest(fixture.Storage.Id, null));

        await Assert.ThrowsAsync<ValidationAppException>(() => fixture.Handovers.ReleaseAsync(
            started.Code!, fixture.Staff.Id, new ReleaseHandoverRequest(OwnerIdChecked: false, null)));

        // Still on the shelf.
        Assert.Equal(HandoverStatus.InCustody, (await fixture.Db.LostReportFoundClaims.SingleAsync()).Status);
    }

    [Fact]
    public async Task ReleasingItRecordsWhoCheckedAndTheCodeStopsWorking()
    {
        await using var fixture = await Fixture.CreateAsync();
        var started = await fixture.Handovers.StartAsync(fixture.Report.Id, fixture.Finder.Id);
        await fixture.Handovers.ReceiveAsync(started.Code!, fixture.Staff.Id, new ReceiveHandoverRequest(fixture.Storage.Id, null));

        var released = await fixture.Handovers.ReleaseAsync(
            started.Code!, fixture.Staff.Id, new ReleaseHandoverRequest(OwnerIdChecked: true, "Student ID checked, name matched."));

        Assert.Equal("Collected", released.Status);

        var claim = await fixture.Db.LostReportFoundClaims.SingleAsync();
        Assert.Equal(fixture.Staff.Id, claim.CollectedByStaffId);
        Assert.Equal("Student ID checked, name matched.", claim.CollectionCheck);
        Assert.Null(claim.HandoverCode);

        Assert.Equal(LostReportStatus.Resolved, (await fixture.Db.LostReports.SingleAsync()).Status);
        Assert.Equal(FoundReportStatus.Returned, (await fixture.Db.FoundReports.SingleAsync()).Status);

        // The owner gets a receipt naming the desk, with the alarm in case it was not them.
        var receipt = await fixture.Db.Notifications.SingleAsync(n => n.UserId == fixture.Owner.Id && n.Type == NotificationType.ItemCollected);
        Assert.Contains(fixture.Storage.Name, receipt.Message);
        Assert.Contains("Wasn't you?", receipt.Message);

        // Once. A spent code is indistinguishable from one that never existed.
        await Assert.ThrowsAsync<NotFoundAppException>(() => fixture.Handovers.LookupAsync(started.Code!));

        // And the finder is credited for the return itself, on top of the hand-in.
        var awards = await fixture.Db.HonorAwards.Where(a => a.UserId == fixture.Finder.Id).ToListAsync();
        Assert.Equal(2, awards.Count);
        Assert.Equal(35, awards.Sum(a => a.Points));
    }

    [Fact]
    public async Task AWalkNobodyCompletesLapsesAndTheNoticeComesBack()
    {
        await using var fixture = await Fixture.CreateAsync();
        var started = await fixture.Handovers.StartAsync(fixture.Report.Id, fixture.Finder.Id);

        // Wind the clock past the window rather than waiting 48 hours for it.
        var claim = await fixture.Db.LostReportFoundClaims.SingleAsync();
        claim.HandoverExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await fixture.Db.SaveChangesAsync();

        // The desk finds nothing, and the report is back on the feed for everyone else.
        await Assert.ThrowsAsync<NotFoundAppException>(() => fixture.Handovers.LookupAsync(started.Code!));
        Assert.Null(await fixture.Handovers.GetForUserAsync(fixture.Report.Id, fixture.Owner.Id));

        var feed = await fixture.Reports.GetPublicFeedAsync(new LostReportQuery());
        Assert.Single(feed.Items);
        Assert.Equal(HandoverStatus.Expired, (await fixture.Db.LostReportFoundClaims.SingleAsync()).Status);
    }

    [Fact]
    public async Task TheFinderCanCallItOffBeforeADeskHasIt()
    {
        await using var fixture = await Fixture.CreateAsync();
        var started = await fixture.Handovers.StartAsync(fixture.Report.Id, fixture.Finder.Id);

        var cancelled = await fixture.Handovers.CancelAsync(fixture.Report.Id, fixture.Finder.Id);
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Null(cancelled.Code);

        var feed = await fixture.Reports.GetPublicFeedAsync(new LostReportQuery());
        Assert.Single(feed.Items);
        await Assert.ThrowsAsync<NotFoundAppException>(() => fixture.Handovers.LookupAsync(started.Code!));

        // The owner was told it was coming, so they are told it is not.
        Assert.Contains(
            await fixture.Db.Notifications.Where(n => n.UserId == fixture.Owner.Id).ToListAsync(),
            n => n.Type == NotificationType.HandoverCancelled);

        // ...but once a desk has it, it is out of the finder's hands.
        await fixture.Handovers.StartAsync(fixture.Report.Id, fixture.Finder.Id);
        var restarted = await fixture.Handovers.GetForUserAsync(fixture.Report.Id, fixture.Finder.Id);
        await fixture.Handovers.ReceiveAsync(restarted!.Code!, fixture.Staff.Id, new ReceiveHandoverRequest(fixture.Storage.Id, null));
        await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Handovers.CancelAsync(fixture.Report.Id, fixture.Finder.Id));
    }

    [Fact]
    public async Task AWrongCodeLooksExactlyLikeAnythingElse()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Handovers.StartAsync(fixture.Report.Id, fixture.Finder.Id);

        await Assert.ThrowsAsync<NotFoundAppException>(() => fixture.Handovers.LookupAsync("000000"));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(
            FoundUDbContext db,
            HandoverService handovers,
            LostReportService reports,
            AppUser owner,
            AppUser finder,
            AppUser other,
            AppUser staff,
            LostReport report,
            StorageLocation storage)
            => (Db, Handovers, Reports, Owner, Finder, Other, Staff, Report, Storage)
                = (db, handovers, reports, owner, finder, other, staff, report, storage);

        public FoundUDbContext Db { get; }
        public HandoverService Handovers { get; }
        public LostReportService Reports { get; }
        public AppUser Owner { get; }
        public AppUser Finder { get; }
        public AppUser Other { get; }
        public AppUser Staff { get; }
        public LostReport Report { get; }
        public StorageLocation Storage { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var db = new FoundUDbContext(new DbContextOptionsBuilder<FoundUDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

            var owner = new AppUser { FullName = "Owner Student", UserName = "owner@test", Email = "owner@test", Role = UserRole.Student, StudentNumber = "IT0001" };
            var finder = new AppUser { FullName = "Finder Student", UserName = "finder@test", Email = "finder@test", Role = UserRole.Student };
            var other = new AppUser { FullName = "Other Student", UserName = "other@test", Email = "other@test", Role = UserRole.Student };
            var staff = new AppUser { FullName = "Desk Staff", UserName = "staff@test", Email = "staff@test", Role = UserRole.Staff };
            var category = new Category { Name = "Bags" };
            var itemType = new ItemType { Name = "Backpack", Category = category };
            var location = new CampusLocation { Name = "Library" };
            var storage = new StorageLocation { Name = "Main Desk" };
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
            db.AddRange(owner, finder, other, staff, category, itemType, location, storage, report);
            await db.SaveChangesAsync();

            var notifications = new NotificationService(db);
            var honor = new HonorService(db);
            return new Fixture(
                db,
                new HandoverService(db, notifications, honor),
                new LostReportService(db, new NoopPhotoStorage(), notifications, new NoopParser(), honor),
                owner, finder, other, staff, report, storage);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class NoopParser : IDescriptionParserAgentClient
    {
        public Task<DescriptionParserAgentCallResult<DescriptionParserAgentResult>> ParseAsync(
            string description, string correlationId, CancellationToken cancellationToken = default)
            => Task.FromResult(DescriptionParserAgentCallResult<DescriptionParserAgentResult>.Failure("not configured"));
    }

    private sealed class NoopPhotoStorage : IPhotoStorage
    {
        public Task<string> SaveAsync(PhotoUpload upload, string folder, CancellationToken cancellationToken = default) => Task.FromResult("/unused");
        public Task DeleteAsync(string url, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
