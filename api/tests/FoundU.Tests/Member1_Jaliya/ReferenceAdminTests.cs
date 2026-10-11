using FoundU.Application.Admin.Dtos;
using FoundU.Application.Common.Exceptions;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Administration;
using FoundU.Infrastructure.Persistence;
using FoundU.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Tests;

/// <summary>
/// An admin looking after the lists everyone picks from. What matters: nothing a report names
/// is ever removed, retiring hides without breaking, nobody is left with an empty picker, and a
/// deleted name can be added again.
/// </summary>
[Trait("Member", "Member1-Jaliya")]
public sealed class ReferenceAdminTests
{
    private static ReferenceItemRequest Named(string name, Guid? categoryId = null, string? building = null)
        => new(name, null, building, null, categoryId, null);

    [Fact]
    public async Task AnAdminCanAddAHallAndItAppearsInThePickers()
    {
        await using var fixture = await Fixture.CreateAsync();

        await fixture.Admin.CreateAsync(ReferenceKind.Locations, Named("New Science Hall", building: "Block C"));

        var pickers = await fixture.Reference.GetCampusLocationsAsync();
        Assert.Contains(pickers, l => l.Name == "New Science Hall" && l.Building == "Block C");
    }

    [Fact]
    public async Task SomethingInUseCannotBeDeletedButCanBeRetired()
    {
        await using var fixture = await Fixture.CreateAsync();

        var refused = await Assert.ThrowsAsync<ConflictAppException>(
            () => fixture.Admin.DeleteAsync(ReferenceKind.Locations, fixture.Library.Id));
        Assert.Contains("Retire it instead", refused.Message);

        // Retiring needs somewhere else to pick - see TheLastAvailableOfAKindCannotBeRetired.
        await fixture.Admin.CreateAsync(ReferenceKind.Locations, Named("Cafeteria"));
        await fixture.Admin.SetActiveAsync(ReferenceKind.Locations, fixture.Library.Id, isActive: false);

        // Gone from the pickers...
        Assert.DoesNotContain(await fixture.Reference.GetCampusLocationsAsync(), l => l.Id == fixture.Library.Id);
        // ...while the report that names it still reads correctly.
        var report = await fixture.Db.LostReports.Include(r => r.LastSeenLocation).SingleAsync();
        Assert.Equal("Library", report.LastSeenLocation.Name);

        // And it can come back.
        await fixture.Admin.SetActiveAsync(ReferenceKind.Locations, fixture.Library.Id, isActive: true);
        Assert.Contains(await fixture.Reference.GetCampusLocationsAsync(), l => l.Id == fixture.Library.Id);
    }

    [Fact]
    public async Task SomethingUnusedIsDeleted()
    {
        await using var fixture = await Fixture.CreateAsync();
        var id = await fixture.Admin.CreateAsync(ReferenceKind.Locations, Named("Old Annex"));

        await fixture.Admin.DeleteAsync(ReferenceKind.Locations, id);

        var all = await fixture.Admin.GetAllAsync();
        Assert.DoesNotContain(all.Locations, l => l.Id == id);
    }

    [Fact]
    public async Task ADeletedNameCanBeAddedAgainAndComesBackAsTheSameRow()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = await fixture.Admin.CreateAsync(ReferenceKind.Locations, Named("Old Annex"));
        await fixture.Admin.DeleteAsync(ReferenceKind.Locations, first);

        // The unique index predates soft delete, so a second "Old Annex" row would collide.
        var again = await fixture.Admin.CreateAsync(ReferenceKind.Locations, Named("old annex"));

        Assert.Equal(first, again);
        Assert.Contains(await fixture.Reference.GetCampusLocationsAsync(), l => l.Id == first);
    }

    [Fact]
    public async Task ADuplicateNameIsRefusedPlainly()
    {
        await using var fixture = await Fixture.CreateAsync();

        await Assert.ThrowsAsync<ConflictAppException>(
            () => fixture.Admin.CreateAsync(ReferenceKind.Categories, Named("bags")));
    }

    [Fact]
    public async Task TheLastAvailableOfAKindCannotBeRetired()
    {
        await using var fixture = await Fixture.CreateAsync();

        // Library is the only campus place - the report form would have nothing to offer.
        await Assert.ThrowsAsync<ConflictAppException>(
            () => fixture.Admin.SetActiveAsync(ReferenceKind.Locations, fixture.Library.Id, isActive: false));

        // Nor the only storage, or the desk has nowhere to log anything.
        await Assert.ThrowsAsync<ConflictAppException>(
            () => fixture.Admin.SetActiveAsync(ReferenceKind.Storage, fixture.Desk.Id, isActive: false));
    }

    [Fact]
    public async Task UsageCountsTellTheAdminWhatIsSafeToDelete()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Admin.CreateAsync(ReferenceKind.ItemTypes, Named("Tote Bag", fixture.Bags.Id));

        var all = await fixture.Admin.GetAllAsync();
        var bags = Assert.Single(all.Categories);
        Assert.Equal(1, bags.UsageCount);
        Assert.Equal(1, bags.ItemTypes.Single(t => t.Name == "Backpack").UsageCount);
        Assert.Equal(0, bags.ItemTypes.Single(t => t.Name == "Tote Bag").UsageCount);
        Assert.Equal(1, all.Locations.Single(l => l.Id == fixture.Library.Id).UsageCount);
    }

    [Fact]
    public async Task DeletingAnUnusedCategoryTakesItsItemTypesWithIt()
    {
        await using var fixture = await Fixture.CreateAsync();
        var category = await fixture.Admin.CreateAsync(ReferenceKind.Categories, Named("Sports Gear"));
        await fixture.Admin.CreateAsync(ReferenceKind.ItemTypes, Named("Racket", category));

        await fixture.Admin.DeleteAsync(ReferenceKind.Categories, category);

        Assert.False(await fixture.Db.ItemTypes.AnyAsync(t => t.Name == "Racket"));
        Assert.True(await fixture.Db.ItemTypes.IgnoreQueryFilters().AnyAsync(t => t.Name == "Racket" && t.IsDeleted));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(FoundUDbContext db, Category bags, CampusLocation library, StorageLocation desk)
            => (Db, Bags, Library, Desk) = (db, bags, library, desk);

        public FoundUDbContext Db { get; }
        public Category Bags { get; }
        public CampusLocation Library { get; }
        public StorageLocation Desk { get; }
        public ReferenceAdminService Admin => new(Db);
        public ReferenceDataService Reference => new(Db);

        public static async Task<Fixture> CreateAsync()
        {
            var db = new FoundUDbContext(new DbContextOptionsBuilder<FoundUDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

            var owner = new AppUser { FullName = "Owner", UserName = "owner@test", Email = "owner@test", Role = UserRole.Student };
            var bags = new Category { Name = "Bags" };
            var backpack = new ItemType { Name = "Backpack", Category = bags };
            var library = new CampusLocation { Name = "Library" };
            var desk = new StorageLocation { Name = "Library Front Desk" };
            db.AddRange(owner, bags, backpack, library, desk, new LostReport
            {
                Student = owner,
                Category = bags,
                ItemType = backpack,
                LastSeenLocation = library,
                Description = "Black backpack with two zips",
                EstimatedLostFromAt = DateTime.UtcNow.AddHours(-3),
                EstimatedLostToAt = DateTime.UtcNow.AddHours(-1),
            });
            await db.SaveChangesAsync();
            return new Fixture(db, bags, library, desk);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
