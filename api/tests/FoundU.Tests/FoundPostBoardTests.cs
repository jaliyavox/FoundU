using FoundU.Application.Abstractions;
using FoundU.Application.Common.Exceptions;
using FoundU.Application.FoundReports.Dtos;
using FoundU.Application.Matching.Dtos;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Honor;
using FoundU.Infrastructure.Matching;
using FoundU.Infrastructure.Notifications;
using FoundU.Infrastructure.Persistence;
using FoundU.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FoundU.Tests;

/// <summary>
/// A finder's post on the found board stays up until its owner has the item back - through
/// the walk to the desk, storage, and an approved claim. Collection takes it down.
/// </summary>
public sealed class FoundPostBoardTests
{
    [Fact]
    public async Task APostStaysOnTheBoardUntilTheOwnerCollectsIt()
    {
        await using var fixture = await Fixture.CreateAsync();
        var post = await fixture.Posts.PostAsync(fixture.NewPost(), fixture.Finder.Id);

        Assert.Equal("Posted", await StatusOnBoard(fixture, post.Id));

        await fixture.Posts.ConfirmAsync(post.Id, fixture.Staff.Id, new ConfirmFoundPostRequest(fixture.Storage.Id, "Sticker inside the lid", null));
        Assert.Equal("Unclaimed", await StatusOnBoard(fixture, post.Id));

        await SetStatus(fixture, post.Id, FoundReportStatus.Claimed);
        Assert.Equal("Claimed", await StatusOnBoard(fixture, post.Id));

        await SetStatus(fixture, post.Id, FoundReportStatus.Returned);
        Assert.Null(await StatusOnBoard(fixture, post.Id));
    }

    [Fact]
    public async Task AnItemThatNeverWasAPostStaysOffTheBoard()
    {
        await using var fixture = await Fixture.CreateAsync();
        // What a handover receive writes: a finder, at a desk, but never posted.
        var item = new FoundReport
        {
            FinderId = fixture.Finder.Id, StaffId = fixture.Staff.Id, CategoryId = fixture.Category.Id,
            ItemTypeId = fixture.Bottle.Id, FoundLocationId = fixture.Library.Id, StorageLocationId = fixture.Storage.Id,
            GeneralDescription = "Handed over with a code", FoundAt = DateTime.UtcNow, Status = FoundReportStatus.Claimed,
        };
        fixture.Db.FoundReports.Add(item);
        await fixture.Db.SaveChangesAsync();

        Assert.Null(await StatusOnBoard(fixture, item.Id));
    }

    [Fact]
    public async Task TheOwnerCanStillSayItIsTheirsOnceItIsAtADesk()
    {
        await using var fixture = await Fixture.CreateAsync();
        var post = await fixture.Posts.PostAsync(fixture.NewPost(), fixture.Finder.Id);
        await fixture.Posts.ConfirmAsync(post.Id, fixture.Staff.Id, new ConfirmFoundPostRequest(fixture.Storage.Id, null, null));
        var finderNotes = await fixture.Db.Notifications.CountAsync(n => n.UserId == fixture.Finder.Id);

        await fixture.Posts.RecogniseAsync(post.Id, fixture.Owner.Id, new RecogniseFoundPostRequest(fixture.Report.Id));

        // A suggestion they can claim from - and the finder, who already handed it in, is left alone.
        Assert.True(await fixture.Db.MatchSuggestions.AnyAsync(m => m.FoundReportId == post.Id && m.LostReportId == fixture.Report.Id));
        Assert.Equal(finderNotes, await fixture.Db.Notifications.CountAsync(n => n.UserId == fixture.Finder.Id));

        await SetStatus(fixture, post.Id, FoundReportStatus.Claimed);
        var error = await Assert.ThrowsAsync<ConflictAppException>(() =>
            fixture.Posts.RecogniseAsync(post.Id, fixture.Owner.Id, new RecogniseFoundPostRequest(fixture.Report.Id)));
        Assert.Contains("already proved", error.Message);
    }

    private static async Task<string?> StatusOnBoard(Fixture fixture, Guid id)
        => (await fixture.Posts.GetFeedAsync(new FoundPostQuery { PageSize = 50 }, null)).Items.FirstOrDefault(p => p.Id == id)?.Status;

    private static async Task SetStatus(Fixture fixture, Guid id, FoundReportStatus status)
    {
        var item = await fixture.Db.FoundReports.SingleAsync(f => f.Id == id);
        item.Status = status;
        await fixture.Db.SaveChangesAsync();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(FoundUDbContext db) => Db = db;

        public FoundUDbContext Db { get; }
        public FoundPostService Posts { get; private set; } = default!;
        public AppUser Owner { get; private set; } = default!;
        public AppUser Finder { get; private set; } = default!;
        public AppUser Staff { get; private set; } = default!;
        public Category Category { get; private set; } = default!;
        public ItemType Bottle { get; private set; } = default!;
        public CampusLocation Library { get; private set; } = default!;
        public StorageLocation Storage { get; private set; } = default!;
        public LostReport Report { get; private set; } = default!;

        public CreateFoundPostRequest NewPost() =>
            new(Category.Id, Bottle.Id, Library.Id, "Blue bottle by the stairs", "Blue", DateTime.UtcNow.AddMinutes(-30), null);

        public static async Task<Fixture> CreateAsync()
        {
            var db = new FoundUDbContext(new DbContextOptionsBuilder<FoundUDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var f = new Fixture(db)
            {
                Owner = new AppUser { FullName = "Owner Student", UserName = "owner@test", Email = "owner@test", Role = UserRole.Student },
                Finder = new AppUser { FullName = "Finder Student", UserName = "finder@test", Email = "finder@test", Role = UserRole.Student },
                Staff = new AppUser { FullName = "Desk Staff", UserName = "staff@test", Email = "staff@test", Role = UserRole.Staff },
                Category = new Category { Name = "Other" },
                Library = new CampusLocation { Name = "Library" },
                Storage = new StorageLocation { Name = "Main Desk" },
            };
            f.Bottle = new ItemType { Name = "Water Bottle", Category = f.Category };
            f.Report = new LostReport
            {
                Student = f.Owner, Category = f.Category, ItemType = f.Bottle, LastSeenLocation = f.Library,
                Description = "Blue bottle", PrimaryColor = "Blue",
                EstimatedLostFromAt = DateTime.UtcNow.AddHours(-3), EstimatedLostToAt = DateTime.UtcNow.AddHours(-1),
                Status = LostReportStatus.Active,
            };
            db.AddRange(f.Owner, f.Finder, f.Staff, f.Category, f.Bottle, f.Library, f.Storage, f.Report);
            await db.SaveChangesAsync();

            var notifications = new NotificationService(db);
            var suggestions = new MatchSuggestionService(db, notifications, new NoAgent());
            f.Posts = new FoundPostService(db, suggestions, new FoundReportService(db, suggestions), notifications,
                new HonorService(db), NullLogger<FoundPostService>.Instance);
            return f;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class NoAgent : IMatchingAgentClient
    {
        public Task<MatchingAgentCallResult<MatchingAgentRecommendation>> MatchReportsAsync(
            MatchingAgentReportSummary lostReport, MatchingAgentReportSummary foundReport, string correlationId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(MatchingAgentCallResult<MatchingAgentRecommendation>.Failure("Not configured."));
    }
}
