using FoundU.Application.Abstractions;
using FoundU.Application.Common.Exceptions;
using FoundU.Application.Intake;
using FoundU.Application.LostReports.Dtos;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Honor;
using FoundU.Infrastructure.Intake;
using FoundU.Infrastructure.Notifications;
using FoundU.Infrastructure.Persistence;
using FoundU.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Tests;

/// <summary>
/// Ask FoundU from the other side of the counter: a student who has found something.
///
/// The rules that matter: a finder is searched against open lost reports, never their own and
/// never one already paused for a hand-in; they see only what the feed shows; with nobody
/// looking yet they get a found post to review; and a wallet or a phone is steered to a desk.
/// </summary>
public sealed class IntakeFinderTests
{
    [Fact]
    public async Task AFinderIsPointedAtTheOwnerWhoReportedIt()
    {
        await using var fixture = await Fixture.CreateAsync();
        var report = fixture.AddReport(fixture.Owner, fixture.Bottle, "blue", "Blue steel bottle with a dent");
        await fixture.Db.SaveChangesAsync();

        var response = await fixture.Intake.AskAsync(
            new IntakeRequest("I found a blue water bottle in the cafeteria", FinderSlots("Water Bottle", "blue", "Cafeteria")),
            fixture.Finder.Id);

        Assert.Equal("matched", response.Phase);
        Assert.Equal("lost", response.Match!.Kind);
        Assert.Equal(report.Id, response.Match.Id);
        Assert.Equal("Blue steel bottle with a dent", response.Match.Description);
        Assert.Contains("I found this", response.Reply);
        Assert.Equal("found", response.Slots.Intent);
    }

    [Fact]
    public async Task AFinderIsNeverShownTheirOwnReportOrOneAlreadyOnItsWayToADesk()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.AddReport(fixture.Finder, fixture.Bottle, "blue", "My own bottle");
        var paused = fixture.AddReport(fixture.Owner, fixture.Bottle, "blue", "Someone is already bringing this in");
        paused.PausedUntil = DateTime.UtcNow.AddHours(24);
        var withdrawn = fixture.AddReport(fixture.Owner, fixture.Bottle, "blue", "Found it myself");
        withdrawn.Status = LostReportStatus.Withdrawn;
        await fixture.Db.SaveChangesAsync();

        var response = await fixture.Intake.AskAsync(
            new IntakeRequest("I found a blue water bottle", FinderSlots("Water Bottle", "blue", null)),
            fixture.Finder.Id);

        Assert.Equal("no_match", response.Phase);
        Assert.Null(response.Match);
        Assert.Empty(fixture.Agent.LastCandidates!);
    }

    [Fact]
    public async Task WithNobodyLookingYetTheFinderGetsAFoundPostToReview()
    {
        await using var fixture = await Fixture.CreateAsync();

        var response = await fixture.Intake.AskAsync(
            new IntakeRequest("I found a blue water bottle in the cafeteria", FinderSlots("Water Bottle", "blue", "Cafeteria")),
            fixture.Finder.Id);

        Assert.Equal("no_match", response.Phase);
        Assert.Contains("Post it as a found item", response.Reply);
        Assert.Contains("security desk", response.Reply);
        Assert.Equal("Blue water bottle, found at Cafeteria.", response.Draft.Description);
        Assert.Equal(fixture.Bottle.Id, response.Draft.ItemTypeId);
        Assert.Equal(fixture.Cafeteria.Id, response.Draft.LocationId);
        // A bottle is nobody's valuables.
        Assert.DoesNotContain("safest", response.Reply);
    }

    [Fact]
    public async Task AWalletIsSteeredToSecurity()
    {
        await using var fixture = await Fixture.CreateAsync();

        var response = await fixture.Intake.AskAsync(
            new IntakeRequest("I found a black wallet", FinderSlots("Wallet", "black", null)),
            fixture.Finder.Id);

        Assert.Contains("handing it to security is the safest choice", response.Reply);
    }

    [Fact]
    public async Task AFinderIsAskedWhereTheyFoundItNotWhereTheyLeftIt()
    {
        await using var fixture = await Fixture.CreateAsync();

        var response = await fixture.Intake.AskAsync(
            new IntakeRequest("I found a water bottle", FinderSlots("Water Bottle", null, null)),
            fixture.Finder.Id);

        Assert.Equal("collecting", response.Phase);
        Assert.Contains("where did you find it", response.Reply);
    }

    [Fact]
    public async Task WhenTheSideIsNotKnownYetTheAssistantAsks()
    {
        await using var fixture = await Fixture.CreateAsync();

        var response = await fixture.Intake.AskAsync(new IntakeRequest("hello"), fixture.Finder.Id);

        Assert.Equal("collecting", response.Phase);
        Assert.Contains("lose something, or find something", response.Reply);
    }

    [Fact]
    public async Task AnOwnerIsStillSearchedAgainstFoundItems()
    {
        await using var fixture = await Fixture.CreateAsync();
        // An open lost report that a finder would see - an owner must not be shown it.
        fixture.AddReport(fixture.Finder, fixture.Bottle, "blue", "Someone else's bottle");
        await fixture.Db.SaveChangesAsync();

        var response = await fixture.Intake.AskAsync(
            new IntakeRequest("I lost my blue water bottle", new IntakeSlots("Water Bottle", "blue")),
            fixture.Owner.Id);

        Assert.Equal("no_match", response.Phase);
        Assert.Empty(fixture.Agent.LastCandidates!);
    }

    [Fact]
    public void AnIntentOtherThanLostOrFoundIsRejected()
    {
        var validator = new IntakeRequestValidator();

        Assert.False(validator.Validate(new IntakeRequest("hi", new IntakeSlots(Intent: "stolen"))).IsValid);
        Assert.True(validator.Validate(new IntakeRequest("hi", new IntakeSlots(Intent: "found"))).IsValid);
        Assert.True(validator.Validate(new IntakeRequest("hi", new IntakeSlots())).IsValid);
    }

    [Fact]
    public async Task OneFeedReportCanBeOpenedByLinkButOnlyWhileTheFeedWouldShowIt()
    {
        await using var fixture = await Fixture.CreateAsync();
        var open = fixture.AddReport(fixture.Owner, fixture.Bottle, "blue", "Blue bottle");
        var paused = fixture.AddReport(fixture.Owner, fixture.Bottle, "blue", "Paused bottle");
        paused.PausedUntil = DateTime.UtcNow.AddHours(24);
        await fixture.Db.SaveChangesAsync();

        var item = await fixture.Reports.GetPublicFeedItemAsync(open.Id, fixture.Finder.Id);
        Assert.Equal("Blue bottle", item.Description);
        Assert.False(item.IsMine);

        await Assert.ThrowsAsync<NotFoundAppException>(() => fixture.Reports.GetPublicFeedItemAsync(paused.Id));
    }

    private static IntakeSlots FinderSlots(string? itemType, string? colour, string? location)
        => new(itemType, colour, location, Intent: IntakeSlots.Found);

    /// <summary>
    /// Stands in for the Python agent: echoes the slots back when collecting, and picks the
    /// first candidate it is given, as the real one does for a strong match.
    /// </summary>
    private sealed class FakeAgent : IIntakeAgentClient
    {
        public IReadOnlyList<IntakeMatch>? LastCandidates { get; private set; }

        public Task<IntakeAgentResult?> RunAsync(IntakeAgentRequest request, CancellationToken cancellationToken = default)
        {
            if (request.Candidates is null)
                return Task.FromResult<IntakeAgentResult?>(new("collecting", request.Slots, "collecting"));

            LastCandidates = request.Candidates;
            return Task.FromResult<IntakeAgentResult?>(request.Candidates.Count == 0
                ? new("none", request.Slots, "no_match")
                : new("match", request.Slots, "matched", request.Candidates[0].Id.ToString(), 0.9));
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(FoundUDbContext db) => Db = db;

        public FoundUDbContext Db { get; }
        public FakeAgent Agent { get; } = new();
        public IntakeService Intake => new(Db, Agent);
        public LostReportService Reports { get; private set; } = default!;
        public AppUser Owner { get; private set; } = default!;
        public AppUser Finder { get; private set; } = default!;
        public Category Bags { get; private set; } = default!;
        public ItemType Bottle { get; private set; } = default!;
        public CampusLocation Cafeteria { get; private set; } = default!;

        public static async Task<Fixture> CreateAsync()
        {
            var db = new FoundUDbContext(new DbContextOptionsBuilder<FoundUDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var fixture = new Fixture(db)
            {
                Owner = new AppUser { FullName = "Owner Student", UserName = "owner@test", Email = "owner@test", Role = UserRole.Student },
                Finder = new AppUser { FullName = "Finder Student", UserName = "finder@test", Email = "finder@test", Role = UserRole.Student },
            };
            var clothing = new Category { Name = "Clothing & Accessories" };
            fixture.Bags = new Category { Name = "Bags & Wallets" };
            fixture.Bottle = new ItemType { Name = "Water Bottle", Category = clothing };
            var wallet = new ItemType { Name = "Wallet", Category = fixture.Bags };
            fixture.Cafeteria = new CampusLocation { Name = "Cafeteria" };
            db.AddRange(fixture.Owner, fixture.Finder, clothing, fixture.Bags, fixture.Bottle, wallet, fixture.Cafeteria);
            await db.SaveChangesAsync();

            var notifications = new NotificationService(db);
            fixture.Reports = new LostReportService(db, new NoopPhotoStorage(), notifications, new NoopParser(), new HonorService(db));
            return fixture;
        }

        public LostReport AddReport(AppUser student, ItemType type, string colour, string description)
        {
            var report = new LostReport
            {
                Student = student,
                Category = type.Category,
                ItemType = type,
                LastSeenLocation = Cafeteria,
                Description = description,
                PrimaryColor = colour,
                EstimatedLostFromAt = DateTime.UtcNow.AddHours(-5),
                EstimatedLostToAt = DateTime.UtcNow.AddHours(-2),
                Status = LostReportStatus.Active,
            };
            Db.LostReports.Add(report);
            return report;
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
