using FoundU.Application.Abstractions;
using FoundU.Application.Common.Exceptions;
using FoundU.Application.LostReports.Dtos;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Honor;
using FoundU.Infrastructure.Notifications;
using FoundU.Infrastructure.Persistence;
using FoundU.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Tests;

/// <summary>
/// The good ending: the owner closes their own report, the people who helped are thanked and
/// credited, and nobody can earn the same credit twice.
/// </summary>
public sealed class HonorPointsTests
{
    [Fact]
    public async Task ResolvingAReport_CreditsEveryFinderOnceAndTellsThem()
    {
        await using var fixture = await Fixture.CreateAsync();

        await fixture.Service.ResolveAsync(fixture.Report.Id, fixture.Owner.Id, "Handed in at the library desk");

        var report = await fixture.Db.LostReports.SingleAsync();
        Assert.Equal(LostReportStatus.Resolved, report.Status);

        var award = await fixture.Db.HonorAwards.SingleAsync();
        Assert.Equal(fixture.Finder.Id, award.UserId);
        Assert.Equal(HonorAwardReason.HelpedReturn, award.Reason);
        Assert.Equal(25, award.Points);
        Assert.Equal(fixture.Report.Id, award.LostReportId);

        var notification = await fixture.Db.Notifications
            .SingleAsync(n => n.UserId == fixture.Finder.Id && n.Type == NotificationType.ItemReturnedToOwner);
        Assert.Contains("made it back to its owner", notification.Message);

        // The owner earns nothing for closing their own report.
        Assert.False(await fixture.Db.HonorAwards.AnyAsync(a => a.UserId == fixture.Owner.Id));
    }

    [Fact]
    public async Task AnAlreadyClosedReportCannotBeClosedAgainForMorePoints()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.ResolveAsync(fixture.Report.Id, fixture.Owner.Id, null);

        await Assert.ThrowsAsync<ConflictAppException>(
            () => fixture.Service.ResolveAsync(fixture.Report.Id, fixture.Owner.Id, null));

        Assert.Equal(25, await fixture.Db.HonorAwards.SumAsync(a => a.Points));
    }

    [Fact]
    public async Task OnlyTheAuthorCanCloseTheirReport()
    {
        await using var fixture = await Fixture.CreateAsync();

        await Assert.ThrowsAsync<ForbiddenAppException>(
            () => fixture.Service.ResolveAsync(fixture.Report.Id, fixture.Finder.Id, null));

        Assert.False(await fixture.Db.HonorAwards.AnyAsync());
    }

    [Fact]
    public async Task TheSameOutcomeNeverPaysTwice()
    {
        await using var fixture = await Fixture.CreateAsync();
        var honor = new HonorService(fixture.Db);

        Assert.True(await honor.QueueAwardAsync(fixture.Finder.Id, HonorAwardReason.HelpedReturn, fixture.Report.Id, null, "first"));
        // Queued but not yet saved - a second call in the same unit of work must still refuse.
        Assert.False(await honor.QueueAwardAsync(fixture.Finder.Id, HonorAwardReason.HelpedReturn, fixture.Report.Id, null, "again"));
        await fixture.Db.SaveChangesAsync();
        Assert.False(await honor.QueueAwardAsync(fixture.Finder.Id, HonorAwardReason.HelpedReturn, fixture.Report.Id, null, "and again"));

        Assert.Equal(1, await fixture.Db.HonorAwards.CountAsync());
    }

    [Fact]
    public async Task HelpToFindShowsTheCodeToQuoteUntilTheItemIsHome()
    {
        await using var fixture = await Fixture.CreateAsync();
        var page = new HelpToFindService(fixture.Db);

        var before = await page.GetAsync(fixture.Finder.Id);
        var offer = Assert.Single(before.Activity);
        Assert.Equal("found-claim", offer.Kind);
        Assert.Equal(fixture.Report.HandInCode, offer.HandInCode);
        Assert.Equal("Owner", offer.OwnerName);
        Assert.Equal(0, before.HonorPoints);

        await fixture.Service.ResolveAsync(fixture.Report.Id, fixture.Owner.Id, null);

        var after = await page.GetAsync(fixture.Finder.Id);
        Assert.Equal(25, after.HonorPoints);
        Assert.Equal(1, after.ItemsReturned);
        // The code has done its job, so it stops being shown.
        Assert.Null(Assert.Single(after.Activity).HandInCode);
        Assert.Equal(0, after.OpenHelpOffers);
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

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(FoundUDbContext db, LostReportService service, AppUser owner, AppUser finder, LostReport report)
            => (Db, Service, Owner, Finder, Report) = (db, service, owner, finder, report);

        public FoundUDbContext Db { get; }
        public LostReportService Service { get; }
        public AppUser Owner { get; }
        public AppUser Finder { get; }
        public LostReport Report { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var db = new FoundUDbContext(new DbContextOptionsBuilder<FoundUDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

            var owner = new AppUser { FullName = "Owner Student", UserName = "owner@test", Role = UserRole.Student };
            var finder = new AppUser { FullName = "Finder Student", UserName = "finder@test", Role = UserRole.Student };
            var category = new Category { Name = "Bags" };
            var itemType = new ItemType { Name = "Backpack", Category = category };
            var location = new CampusLocation { Name = "Library" };
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
            db.AddRange(owner, finder, category, itemType, location, report);
            await db.SaveChangesAsync();

            db.LostReportFoundClaims.Add(new LostReportFoundClaim { LostReportId = report.Id, FinderId = finder.Id });
            await db.SaveChangesAsync();

            var service = new LostReportService(db, new NoopPhotoStorage(), new NotificationService(db), new NoopParser(), new HonorService(db));
            return new Fixture(db, service, owner, finder, report);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
