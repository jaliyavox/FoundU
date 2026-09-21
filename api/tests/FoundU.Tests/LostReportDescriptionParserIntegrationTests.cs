using System.Text.Json;
using FoundU.Application.Abstractions;
using FoundU.Application.LostReports.Dtos;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Notifications;
using FoundU.Infrastructure.Persistence;
using FoundU.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Tests;

public sealed class LostReportDescriptionParserIntegrationTests
{
    private const string Description = "Blue backpack with a SECRET-OWNERSHIP-DETAIL-DO-NOT-LEAK keychain";

    [Fact]
    public async Task ValidParse_EnrichesExistingAttributesWithoutReplacingStudentInput()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Parser.Result = DescriptionParserAgentCallResult<DescriptionParserAgentResult>.Success(
            new("Backpack", "Blue", null, ["Small keychain"], .9m, "parser-run-1"));

        var created = await fixture.Service.CreateAsync(fixture.Request, fixture.Student.Id);
        var report = fixture.Db.LostReports.Single();

        Assert.Equal(Description, report.Description);
        Assert.Equal("Red", report.PrimaryColor); // Explicit student selection wins over AI output.
        Assert.Equal(fixture.ItemType.Id, report.ItemTypeId);
        Assert.Contains("Small keychain", report.ParsedAttributesJson!);
        Assert.Equal(LostReportStatus.Active, report.Status);
        Assert.False(report.IsFlagged);
        Assert.Equal(Description, fixture.Parser.LastDescription);
        Assert.DoesNotContain(Description, fixture.Db.AgentRuns.Single().FinalOutcomeJson!);
        Assert.DoesNotContain("Small keychain", fixture.Db.AgentRuns.Single().FinalOutcomeJson!);
        Assert.Equal(created.Description, report.Description);
    }

    [Fact]
    public async Task ParserFailure_StillCreatesNormalReportAndStoresOnlyFallbackAudit()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Parser.Result = DescriptionParserAgentCallResult<DescriptionParserAgentResult>.Failure("raw provider details");

        var created = await fixture.Service.CreateAsync(fixture.Request, fixture.Student.Id);
        var report = fixture.Db.LostReports.Single();
        var audit = fixture.Db.AgentRuns.Single();

        Assert.Equal(report.Id, created.Id);
        Assert.Equal(Description, report.Description);
        Assert.Null(report.ParsedAttributesJson);
        Assert.Equal(LostReportStatus.Active, report.Status);
        Assert.Equal(AgentRunStatus.Failed, audit.Status);
        Assert.DoesNotContain(Description, audit.FinalOutcomeJson!);
        Assert.DoesNotContain("raw provider details", audit.ErrorMessage!);
    }

    [Fact]
    public async Task PublicFeed_RemainsReducedAndDoesNotExposeParsedAttributes()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Parser.Result = DescriptionParserAgentCallResult<DescriptionParserAgentResult>.Success(
            new("Backpack", "Blue", null, ["Small keychain"], .9m, "parser-run-2"));
        await fixture.Service.CreateAsync(fixture.Request, fixture.Student.Id);

        var feed = await fixture.Service.GetPublicFeedAsync(new LostReportQuery());
        var serialized = JsonSerializer.Serialize(feed.Items.Single());

        Assert.DoesNotContain("parsedAttributes", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Small keychain", serialized);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(FoundUDbContext db, LostReportService service, FakeParser parser, AppUser student, ItemType itemType, CreateLostReportRequest request)
            => (Db, Service, Parser, Student, ItemType, Request) = (db, service, parser, student, itemType, request);
        public FoundUDbContext Db { get; }
        public LostReportService Service { get; }
        public FakeParser Parser { get; }
        public AppUser Student { get; }
        public ItemType ItemType { get; }
        public CreateLostReportRequest Request { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var db = new FoundUDbContext(new DbContextOptionsBuilder<FoundUDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var student = new AppUser { FullName = "Student", UserName = "student@test", Role = UserRole.Student };
            var category = new Category { Name = "Bags" };
            var itemType = new ItemType { Name = "Backpack", Category = category };
            var location = new CampusLocation { Name = "Library" };
            db.AddRange(student, category, itemType, location);
            await db.SaveChangesAsync();
            var parser = new FakeParser();
            var service = new LostReportService(db, new NoopPhotoStorage(), new NotificationService(db), parser);
            return new Fixture(db, service, parser, student, itemType,
                new(category.Id, itemType.Id, location.Id, Description, "Red", null, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow));
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class FakeParser : IDescriptionParserAgentClient
    {
        public string? LastDescription { get; private set; }
        public DescriptionParserAgentCallResult<DescriptionParserAgentResult> Result { get; set; }
            = DescriptionParserAgentCallResult<DescriptionParserAgentResult>.Failure("not configured");
        public Task<DescriptionParserAgentCallResult<DescriptionParserAgentResult>> ParseAsync(string description, string correlationId, CancellationToken cancellationToken = default)
        {
            LastDescription = description;
            return Task.FromResult(Result);
        }
    }

    private sealed class NoopPhotoStorage : IPhotoStorage
    {
        public Task<string> SaveAsync(PhotoUpload upload, string folder, CancellationToken cancellationToken = default) => Task.FromResult("/unused");
        public Task DeleteAsync(string url, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
