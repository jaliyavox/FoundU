using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FoundU.Application.Abstractions;
using FoundU.Application.Common.Pagination;
using FoundU.Application.LostReports.Dtos;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FoundU.Tests;

public sealed class LostReportsEndpointIntegrationTests
{
    [Fact]
    public async Task CreateAndTrackReport_EnforcesOwnershipAndWithdrawsFromPublicFeed()
    {
        await using var app = await LostReportsHttpApp.CreateAsync();
        using var student = app.ClientFor(app.Student);
        using var finder = app.ClientFor(app.OtherStudent);
        using var staff = app.ClientFor(app.Staff);
        using var anonymous = app.Factory.CreateClient();
        var request = app.CreateRequest("Blue backpack with a red keychain");

        var anonymousCreate = await anonymous.PostAsJsonAsync("/api/lost-reports", request);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousCreate.StatusCode);

        var staffCreate = await staff.PostAsJsonAsync("/api/lost-reports", request);
        Assert.Equal(HttpStatusCode.Forbidden, staffCreate.StatusCode);

        var createdResponse = await student.PostAsJsonAsync("/api/lost-reports", request);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<LostReportDetailDto>();
        Assert.NotNull(created);
        Assert.Equal(nameof(LostReportStatus.Active), created!.Status);
        Assert.Equal("Red", created.PrimaryColor);
        Assert.Contains("Small keychain", created.ParsedAttributesJson);
        Assert.Equal(request.Description, app.Parser.LastDescription);

        var publicFeed = await anonymous.GetFromJsonAsync<PagedResult<LostReportFeedItemDto>>(
            "/api/lost-reports/feed");
        Assert.Contains(publicFeed!.Items, item => item.Id == created.Id);
        Assert.DoesNotContain("StudentId", await (await anonymous.GetAsync("/api/lost-reports/feed"))
            .Content.ReadAsStringAsync());

        var otherStudentDetail = await finder.GetAsync($"/api/lost-reports/{created.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, otherStudentDetail.StatusCode);
        var otherStudentWithdraw = await finder.PostAsJsonAsync(
            $"/api/lost-reports/{created.Id}/withdraw", new WithdrawLostReportRequest("not mine"));
        Assert.Equal(HttpStatusCode.Forbidden, otherStudentWithdraw.StatusCode);
        var otherStudentReports = await finder.GetFromJsonAsync<PagedResult<LostReportListItemDto>>(
            "/api/lost-reports/mine?page=1&pageSize=20");
        Assert.Empty(otherStudentReports!.Items);

        var firstSignal = await finder.PostAsync($"/api/lost-reports/{created.Id}/found-claims", null);
        firstSignal.EnsureSuccessStatusCode();
        var repeatedSignal = await finder.PostAsync($"/api/lost-reports/{created.Id}/found-claims", null);
        repeatedSignal.EnsureSuccessStatusCode();
        var mine = await student.GetFromJsonAsync<PagedResult<LostReportListItemDto>>(
            "/api/lost-reports/mine?page=1&pageSize=20");
        var tracked = Assert.Single(mine!.Items);
        Assert.Equal(1, tracked.FoundClaimCount);

        var withdrawnResponse = await student.PostAsJsonAsync(
            $"/api/lost-reports/{created.Id}/withdraw", new WithdrawLostReportRequest("Found another one"));
        Assert.Equal(HttpStatusCode.OK, withdrawnResponse.StatusCode);
        var withdrawn = await withdrawnResponse.Content.ReadFromJsonAsync<LostReportDetailDto>();
        Assert.Equal(nameof(LostReportStatus.Withdrawn), withdrawn!.Status);

        var feedItem = await anonymous.GetAsync($"/api/lost-reports/feed/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, feedItem.StatusCode);
        var updatedMine = await student.GetFromJsonAsync<PagedResult<LostReportListItemDto>>(
            "/api/lost-reports/mine?page=1&pageSize=20");
        Assert.Equal(nameof(LostReportStatus.Withdrawn), Assert.Single(updatedMine!.Items).Status);
        Assert.True(await app.HasStatusHistoryAsync(created.Id, LostReportStatus.Withdrawn));
    }

    [Fact]
    public async Task OwnerCanResolveReport_AndResolvedReportCannotBeResolvedAgain()
    {
        await using var app = await LostReportsHttpApp.CreateAsync();
        using var student = app.ClientFor(app.Student);
        using var anonymous = app.Factory.CreateClient();
        var create = await student.PostAsJsonAsync(
            "/api/lost-reports", app.CreateRequest("Black wallet with a silver logo"));
        create.EnsureSuccessStatusCode();
        var report = (await create.Content.ReadFromJsonAsync<LostReportDetailDto>())!;

        var resolvedResponse = await student.PostAsJsonAsync(
            $"/api/lost-reports/{report.Id}/resolve", new ResolveLostReportRequest("Returned by security"));
        Assert.Equal(HttpStatusCode.OK, resolvedResponse.StatusCode);
        var resolved = await resolvedResponse.Content.ReadFromJsonAsync<LostReportDetailDto>();
        Assert.Equal(nameof(LostReportStatus.Resolved), resolved!.Status);

        var duplicateResolve = await student.PostAsJsonAsync(
            $"/api/lost-reports/{report.Id}/resolve", new ResolveLostReportRequest(null));
        Assert.Equal(HttpStatusCode.Conflict, duplicateResolve.StatusCode);
        var publicFeed = await anonymous.GetFromJsonAsync<PagedResult<LostReportFeedItemDto>>(
            "/api/lost-reports/feed");
        Assert.DoesNotContain(publicFeed!.Items, item => item.Id == report.Id);
        Assert.True(await app.HasStatusHistoryAsync(report.Id, LostReportStatus.Resolved));
    }

    private sealed class LostReportsHttpApp : IAsyncDisposable
    {
        private LostReportsHttpApp(
            LostReportsWebApplicationFactory factory,
            TestDescriptionParser parser,
            AppUser student,
            AppUser otherStudent,
            AppUser staff,
            Category category,
            ItemType itemType,
            CampusLocation location)
            => (Factory, Parser, Student, OtherStudent, Staff, Category, ItemType, Location) =
                (factory, parser, student, otherStudent, staff, category, itemType, location);

        public LostReportsWebApplicationFactory Factory { get; }
        public TestDescriptionParser Parser { get; }
        public AppUser Student { get; }
        public AppUser OtherStudent { get; }
        public AppUser Staff { get; }
        private Category Category { get; }
        private ItemType ItemType { get; }
        private CampusLocation Location { get; }

        public static async Task<LostReportsHttpApp> CreateAsync()
        {
            var parser = new TestDescriptionParser();
            var factory = new LostReportsWebApplicationFactory(parser);
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FoundUDbContext>();
            var student = new AppUser
            {
                FullName = "Report Owner",
                UserName = "report.owner@test",
                Email = "report.owner@test",
                Role = UserRole.Student,
            };
            var otherStudent = new AppUser
            {
                FullName = "Other Student",
                UserName = "report.finder@test",
                Email = "report.finder@test",
                Role = UserRole.Student,
            };
            var staff = new AppUser
            {
                FullName = "Report Staff",
                UserName = "report.staff@test",
                Email = "report.staff@test",
                Role = UserRole.Staff,
            };
            var category = new Category { Name = "Bags" };
            var itemType = new ItemType { Name = "Backpack", Category = category };
            var location = new CampusLocation { Name = "Library" };
            db.AddRange(student, otherStudent, staff, category, itemType, location);
            await db.SaveChangesAsync();
            return new LostReportsHttpApp(factory, parser, student, otherStudent, staff, category, itemType, location);
        }

        public CreateLostReportRequest CreateRequest(string description)
            => new(
                Category.Id,
                ItemType.Id,
                Location.Id,
                description,
                "Red",
                null,
                DateTime.UtcNow.AddHours(-2),
                DateTime.UtcNow.AddHours(-1));

        public HttpClient ClientFor(AppUser user)
        {
            var client = Factory.CreateClient();
            using var scope = Factory.Services.CreateScope();
            var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", tokens.GenerateAccessToken(user).Value);
            return client;
        }

        public async Task<bool> HasStatusHistoryAsync(Guid reportId, LostReportStatus status)
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FoundUDbContext>();
            return await db.LostReportStatusHistories.AnyAsync(
                history => history.LostReportId == reportId && history.ToStatus == status);
        }

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }

    private sealed class LostReportsWebApplicationFactory(TestDescriptionParser parser)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            foreach (var setting in AuthTestApp.Settings)
                builder.UseSetting(setting.Key, setting.Value);
            builder.ConfigureServices(services =>
            {
                AuthTestApp.ReplaceDatabase(services, $"foundu-lost-reports-http-{Guid.NewGuid():N}");
                services.RemoveAll<IDescriptionParserAgentClient>();
                services.AddSingleton<IDescriptionParserAgentClient>(parser);
            });
        }
    }

    private sealed class TestDescriptionParser : IDescriptionParserAgentClient
    {
        public string? LastDescription { get; private set; }

        public Task<DescriptionParserAgentCallResult<DescriptionParserAgentResult>> ParseAsync(
            string description,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            LastDescription = description;
            return Task.FromResult(DescriptionParserAgentCallResult<DescriptionParserAgentResult>.Success(
                new("Backpack", "Blue", null, ["Small keychain"], .9m, "test-parser-run")));
        }
    }
}
