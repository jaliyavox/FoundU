using FoundU.Application.LostReports.Dtos;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Honor;
using FoundU.Infrastructure.Notifications;
using FoundU.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;
using static FoundU.Tests.PostgresTestSupport;

namespace FoundU.Tests;

/// <summary>Lost reports, their parsed attributes and status history, on real PostgreSQL.</summary>
[Trait("Category", "PostgreSql")]
[Trait("Member", "Member2-Ranasinghe")]
public sealed class Member2DatabaseTests
{
    [PostgresFact]
    public async Task LostReportParserAttributesAndLifecycleHistoryPersistAndResolvedReportLeavesFeed()
    {
        await using var db = PostgresTestDatabase.CreateContext();
        await PostgresTestDatabase.MigrateAsync(db);
        var suffix = Guid.NewGuid().ToString("N");
        var student = NewUser($"report-owner-{suffix}");
        var category = new Category { Name = $"Report category {suffix}" };
        var itemType = new ItemType { Name = $"Report item {suffix}", Category = category };
        var location = new CampusLocation { Name = $"Report location {suffix}" };
        db.AddRange(student, category, itemType, location);
        await db.SaveChangesAsync();

        var description = $"Blue item {suffix} with a red keychain";
        var parser = new SuccessfulDescriptionParser();
        var service = new LostReportService(
            db,
            new NoopPhotoStorage(),
            new NotificationService(db),
            parser,
            new HonorService(db));
        var created = await service.CreateAsync(
            new CreateLostReportRequest(
                category.Id,
                itemType.Id,
                location.Id,
                description,
                null,
                null,
                DateTime.UtcNow.AddHours(-2),
                DateTime.UtcNow.AddHours(-1)),
            student.Id);

        db.ChangeTracker.Clear();
        var persistedReport = await db.LostReports.SingleAsync(report => report.Id == created.Id);
        Assert.Equal(LostReportStatus.Active, persistedReport.Status);
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("""{"itemType":"Backpack","primaryColor":"Blue","secondaryColor":"Red","identifyingFeatures":["Red keychain"],"confidenceScore":0.9}"""),
            JsonNode.Parse(persistedReport.ParsedAttributesJson!)));
        Assert.Single(await db.AgentRuns.Where(run => run.TriggerEntityId == created.Id).ToListAsync());
        Assert.Contains(
            await db.LostReportStatusHistories.Where(history => history.LostReportId == created.Id).ToListAsync(),
            history => history.FromStatus == LostReportStatus.Active
                && history.ToStatus == LostReportStatus.Active
                && history.Reason == "Report submitted");

        await service.ResolveAsync(created.Id, student.Id, "Returned by security");
        db.ChangeTracker.Clear();
        var resolvedReport = await db.LostReports.SingleAsync(report => report.Id == created.Id);
        Assert.Equal(LostReportStatus.Resolved, resolvedReport.Status);
        Assert.Contains(
            await db.LostReportStatusHistories.Where(history => history.LostReportId == created.Id).ToListAsync(),
            history => history.FromStatus == LostReportStatus.Active
                && history.ToStatus == LostReportStatus.Resolved
                && history.Reason == "Returned by security");
        Assert.DoesNotContain(
            (await service.GetPublicFeedAsync(new LostReportQuery())).Items,
            item => item.Id == created.Id);
    }
}
