using FoundU.Application.Common.Exceptions;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Handovers;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Tests;

/// <summary>
/// The one code box at the desk. Whichever of the three codes a finder quotes, it says what
/// the code is - and only for things still open.
/// </summary>
public sealed class DeskCodeTests
{
    [Fact]
    public async Task EachKindOfCodeIsRecognised()
    {
        await using var fixture = await Fixture.CreateAsync();

        var post = Assert.Single(await fixture.Codes.ResolveAsync("111111"));
        Assert.Equal("found-post", post.Kind);
        Assert.Equal(fixture.Post.Id, post.Id);

        var handover = Assert.Single(await fixture.Codes.ResolveAsync("222 222"));
        Assert.Equal("handover", handover.Kind);
        Assert.Equal(fixture.Report.Id, handover.Id);
        Assert.Contains("Finder Student", handover.Detail);

        var report = Assert.Single(await fixture.Codes.ResolveAsync("333333"));
        Assert.Equal("lost-report", report.Kind);
        Assert.Equal(fixture.Report.Id, report.Id);
        // Enough to start logging the item against it.
        Assert.Equal(fixture.Report.ItemTypeId, report.ItemTypeId);
        Assert.Equal(fixture.Report.CategoryId, report.CategoryId);
    }

    [Fact]
    public async Task ClosedThingsAreNotPulledUp()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Post.Status = FoundReportStatus.Unclaimed;
        fixture.Report.Status = LostReportStatus.Resolved;
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundAppException>(() => fixture.Codes.ResolveAsync("111111"));
        await Assert.ThrowsAsync<NotFoundAppException>(() => fixture.Codes.ResolveAsync("333333"));
    }

    [Fact]
    public async Task AnExpiredHandoverIsOver()
    {
        await using var fixture = await Fixture.CreateAsync();
        var handover = await fixture.Db.LostReportFoundClaims.SingleAsync();
        handover.HandoverExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundAppException>(() => fixture.Codes.ResolveAsync("222222"));
    }

    [Fact]
    public async Task ACodeThatMeansTwoThingsShowsBoth()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Post.HandInCode = "333333";
        await fixture.Db.SaveChangesAsync();

        var kinds = (await fixture.Codes.ResolveAsync("333333")).Select(m => m.Kind).Order();

        Assert.Equal(["found-post", "lost-report"], kinds);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("abcdef")]
    public async Task AnythingButSixDigitsIsRefused(string code)
    {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<ValidationAppException>(() => fixture.Codes.ResolveAsync(code));
    }

    [Fact]
    public async Task AnUnknownCodeSaysWhatToDoNext()
    {
        await using var fixture = await Fixture.CreateAsync();
        var error = await Assert.ThrowsAsync<NotFoundAppException>(() => fixture.Codes.ResolveAsync("999999"));
        Assert.Contains("log the item without one", error.Message);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(FoundUDbContext db, FoundReport post, LostReport report)
            => (Db, Post, Report) = (db, post, report);

        public FoundUDbContext Db { get; }
        public FoundReport Post { get; }
        public LostReport Report { get; }
        public DeskCodeService Codes => new(Db);

        public static async Task<Fixture> CreateAsync()
        {
            var db = new FoundUDbContext(new DbContextOptionsBuilder<FoundUDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var owner = new AppUser { FullName = "Owner Student", UserName = "owner@test", Email = "owner@test", Role = UserRole.Student };
            var finder = new AppUser { FullName = "Finder Student", UserName = "finder@test", Email = "finder@test", Role = UserRole.Student };
            var category = new Category { Name = "Bags" };
            var type = new ItemType { Name = "Backpack", Category = category };
            var library = new CampusLocation { Name = "Library" };
            var report = new LostReport
            {
                Student = owner, Category = category, ItemType = type, LastSeenLocation = library,
                Description = "Black backpack", PrimaryColor = "Black", HandInCode = "333333",
                EstimatedLostFromAt = DateTime.UtcNow.AddHours(-4), EstimatedLostToAt = DateTime.UtcNow.AddHours(-2),
                Status = LostReportStatus.Active,
            };
            var post = new FoundReport
            {
                Finder = finder, Category = category, ItemType = type, FoundLocation = library,
                GeneralDescription = "Blue backpack", PrimaryColor = "Blue", HandInCode = "111111",
                FoundAt = DateTime.UtcNow.AddHours(-1), Status = FoundReportStatus.Posted,
            };
            var handover = new LostReportFoundClaim
            {
                LostReport = report, Finder = finder, Status = HandoverStatus.AwaitingHandIn,
                HandoverCode = "222222", HandoverStartedAt = DateTime.UtcNow, HandoverExpiresAt = DateTime.UtcNow.AddHours(48),
            };
            db.AddRange(owner, finder, category, type, library, report, post, handover);
            await db.SaveChangesAsync();
            return new Fixture(db, post, report);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
