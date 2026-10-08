using FoundU.Application.FoundReports.Dtos;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence;
using FoundU.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Tests;

/// <summary>"In storage" is what the desk is physically holding, whatever stage the owner is at.</summary>
public sealed class StorageViewTests
{
    [Fact]
    public async Task InStorageHoldsWaitingAndCollectingItemsButNotReturnedOnes()
    {
        await using var db = new FoundUDbContext(new DbContextOptionsBuilder<FoundUDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var category = new Category { Name = "Bags" };
        var type = new ItemType { Name = "Backpack", Category = category };
        var place = new CampusLocation { Name = "Library" };
        var shelf = new StorageLocation { Name = "Main Desk" };
        FoundReport Item(string note, FoundReportStatus status) => new()
        {
            Category = category, ItemType = type, FoundLocation = place, StorageLocation = shelf,
            GeneralDescription = note, FoundAt = DateTime.UtcNow, Status = status,
        };
        db.AddRange(
            Item("logged by hand, waiting for its owner", FoundReportStatus.Unclaimed),
            // What a handover code's receive writes: the owner is known, it is on the shelf.
            Item("taken in by handover code", FoundReportStatus.Claimed),
            Item("already collected", FoundReportStatus.Returned),
            Item("still with the finder", FoundReportStatus.Posted));
        await db.SaveChangesAsync();

        var service = new FoundReportService(db, suggestions: null!);
        var inStorage = await service.SearchAsync(new FoundReportQuery { Status = FoundReportQuery.InStorage, PageSize = 20 });

        Assert.Equal(
            ["logged by hand, waiting for its owner", "taken in by handover code"],
            inStorage.Items.Select(i => i.GeneralDescription).Order());
    }
}
