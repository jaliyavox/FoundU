using System.Text.Json;
using FoundU.Application.FoundReports.Dtos;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Honor;
using FoundU.Infrastructure.Notifications;
using FoundU.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using static FoundU.Tests.PostgresTestSupport;

namespace FoundU.Tests;

/// <summary>Found items logged at the desk and their match suggestions, on real PostgreSQL.</summary>
[Trait("Category", "PostgreSql")]
[Trait("Member", "Member3-Uthpala")]
public sealed class Member3DatabaseTests
{
    [PostgresFact]
    public async Task DeskLoggedItemPersistsStorageHistoryAndHiddenDetailButTheFoundBoardNeverShowsIt()
    {
        await using var db = PostgresTestDatabase.CreateContext();
        await PostgresTestDatabase.MigrateAsync(db);
        var (staff, category, itemType, location, storage, tag) = await SeedDeskAsync(db);
        var hidden = $"Engraved initials JK {tag} under the strap";
        var reports = new FoundReportService(db, suggestions: null!);

        var created = await reports.CreateAsync(
            new CreateFoundReportRequest(
                category.Id, itemType.Id, location.Id, storage.Id,
                $"Black umbrella {tag}", hidden, "Black", null,
                DateTime.UtcNow.AddMinutes(-30), HandInCode: null),
            staff.Id);

        // What the desk wrote is what PostgreSQL kept: shelf, hidden detail, opening history row.
        db.ChangeTracker.Clear();
        var row = await db.FoundReports.SingleAsync(f => f.Id == created.Id);
        Assert.Equal(storage.Id, row.StorageLocationId);
        Assert.Equal(staff.Id, row.StaffId);
        Assert.Equal(FoundReportStatus.Unclaimed, row.Status);
        Assert.Equal(hidden, row.PrivateVerificationDetails);
        var history = Assert.Single(await db.FoundReportStatusHistories.Where(h => h.FoundReportId == created.Id).ToListAsync());
        Assert.Equal("Item logged", history.Reason);
        Assert.Equal(staff.Id, history.ChangedByUserId);

        // Students find it on the Found board, searched in PostgreSQL (ILIKE), without the hidden detail.
        var board = new FoundPostService(db, null!, reports, new NotificationService(db), new HonorService(db),
            NullLogger<FoundPostService>.Instance);
        var student = NewUser($"board-reader-{tag}");
        db.Users.Add(student);
        await db.SaveChangesAsync();
        var feed = await board.GetFeedAsync(new FoundPostQuery { Search = tag }, student.Id);

        var shown = Assert.Single(feed.Items);
        Assert.Equal(created.Id, shown.Id);
        Assert.Equal(storage.Name, shown.StorageLocationName);
        Assert.DoesNotContain("Engraved initials", JsonSerializer.Serialize(feed));
    }

    [PostgresFact]
    public async Task TwoDesksSavingTheSameFoundReportAtOnceTheSecondGetsAConcurrencyConflict()
    {
        await using var setup = PostgresTestDatabase.CreateContext();
        await PostgresTestDatabase.MigrateAsync(setup);
        var (staff, category, itemType, location, storage, tag) = await SeedDeskAsync(setup);
        var otherShelf = new StorageLocation { Name = $"Overflow shelf {tag}" };
        var item = new FoundReport
        {
            StaffId = staff.Id, CategoryId = category.Id, ItemTypeId = itemType.Id, FoundLocationId = location.Id,
            StorageLocationId = storage.Id, GeneralDescription = $"Blue bottle {tag}", FoundAt = DateTime.UtcNow,
            Status = FoundReportStatus.Unclaimed,
        };
        setup.AddRange(otherShelf, item);
        await setup.SaveChangesAsync();

        // Both desks open the same item, each reads the same xmin row version.
        await using var deskA = PostgresTestDatabase.CreateContext();
        await using var deskB = PostgresTestDatabase.CreateContext();
        var atA = await deskA.FoundReports.SingleAsync(f => f.Id == item.Id);
        var atB = await deskB.FoundReports.SingleAsync(f => f.Id == item.Id);

        atA.StorageLocationId = otherShelf.Id;
        await deskA.SaveChangesAsync();
        atB.GeneralDescription = $"Blue bottle {tag}, dented lid";

        // The second save must not silently overwrite the first one's move.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => deskB.SaveChangesAsync());
        await using var verify = PostgresTestDatabase.CreateContext();
        var saved = await verify.FoundReports.SingleAsync(f => f.Id == item.Id);
        Assert.Equal(otherShelf.Id, saved.StorageLocationId);
        Assert.Equal($"Blue bottle {tag}", saved.GeneralDescription);
    }

    [PostgresFact]
    public async Task OneLostReportAndFoundItemPairHasAtMostOneMatchSuggestion()
    {
        await using var db = PostgresTestDatabase.CreateContext();
        await PostgresTestDatabase.MigrateAsync(db);
        var (staff, category, itemType, location, storage, tag) = await SeedDeskAsync(db);
        var owner = NewUser($"pair-owner-{tag}");
        var lost = new LostReport
        {
            Student = owner, CategoryId = category.Id, ItemTypeId = itemType.Id, LastSeenLocationId = location.Id,
            Description = $"Green scarf {tag}", EstimatedLostFromAt = DateTime.UtcNow.AddHours(-3),
            EstimatedLostToAt = DateTime.UtcNow.AddHours(-2), Status = LostReportStatus.Active,
        };
        var found = new FoundReport
        {
            StaffId = staff.Id, CategoryId = category.Id, ItemTypeId = itemType.Id, FoundLocationId = location.Id,
            StorageLocationId = storage.Id, GeneralDescription = $"Green scarf {tag}", FoundAt = DateTime.UtcNow,
            Status = FoundReportStatus.Unclaimed,
        };
        db.AddRange(lost, found, new MatchSuggestion { LostReport = lost, FoundReport = found, MatchScore = 0.82m });
        await db.SaveChangesAsync();

        // The matching agent and a member of staff linking the same pair from another connection.
        await using var second = PostgresTestDatabase.CreateContext();
        second.MatchSuggestions.Add(new MatchSuggestion
        {
            LostReportId = lost.Id, FoundReportId = found.Id, MatchScore = 1.0m, StaffNote = "Linked by hand",
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
        await using var verify = PostgresTestDatabase.CreateContext();
        var kept = Assert.Single(await verify.MatchSuggestions
            .Where(m => m.LostReportId == lost.Id && m.FoundReportId == found.Id).ToListAsync());
        Assert.Equal(0.82m, kept.MatchScore);
    }

    private static async Task<(AppUser Staff, Category Category, ItemType ItemType, CampusLocation Location, StorageLocation Storage, string Tag)> SeedDeskAsync(
        Infrastructure.Persistence.FoundUDbContext db)
    {
        var tag = Guid.NewGuid().ToString("N");
        var staff = NewUser($"desk-{tag}", UserRole.Staff);
        var category = new Category { Name = $"Desk category {tag}" };
        var itemType = new ItemType { Name = $"Desk item {tag}", Category = category };
        var location = new CampusLocation { Name = $"Desk location {tag}" };
        var storage = new StorageLocation { Name = $"Desk shelf {tag}" };
        db.AddRange(staff, category, itemType, location, storage);
        await db.SaveChangesAsync();
        return (staff, category, itemType, location, storage, tag);
    }
}
