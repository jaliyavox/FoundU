using FoundU.Application.Abstractions;
using FoundU.Application.Admin.Dtos;
using FoundU.Application.Common.Exceptions;
using FoundU.Domain.Common;
using FoundU.Domain.Entities;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Infrastructure.Administration;

/// <summary>
/// The lists everyone else picks from: categories, item types, campus locations, storage.
///
/// Three rules hold throughout:
/// - A row that reports already name is never removed. It can be retired - gone from the
///   pickers, still readable on every record that names it - and restored later.
/// - Delete is only for a row nothing uses, and it is a soft delete like everywhere else.
/// - At least one of each kind stays available, so nobody is left with an empty picker and
///   no way to report a loss or log an item.
///
/// Names are unique even across deleted rows (the indexes predate soft delete), so adding a
/// name that was deleted brings the old row back rather than failing on the index.
/// </summary>
public class ReferenceAdminService : IReferenceAdminService
{
    private readonly FoundUDbContext _db;

    public ReferenceAdminService(FoundUDbContext db)
    {
        _db = db;
    }

    public async Task<ReferenceAdminDto> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Usage counts in a handful of grouped queries rather than one per row.
        var lostByCategory = await CountBy(_db.LostReports.IgnoreQueryFilters(), r => r.CategoryId, cancellationToken);
        var foundByCategory = await CountBy(_db.FoundReports.IgnoreQueryFilters(), r => r.CategoryId, cancellationToken);
        var lostByType = await CountBy(_db.LostReports.IgnoreQueryFilters(), r => r.ItemTypeId, cancellationToken);
        var foundByType = await CountBy(_db.FoundReports.IgnoreQueryFilters(), r => r.ItemTypeId, cancellationToken);
        var lostByPlace = await CountBy(_db.LostReports.IgnoreQueryFilters(), r => r.LastSeenLocationId, cancellationToken);
        var foundByPlace = await CountBy(_db.FoundReports.IgnoreQueryFilters(), r => r.FoundLocationId, cancellationToken);

        var foundByStorage = await _db.FoundReports.IgnoreQueryFilters()
            .Where(r => r.StorageLocationId != null)
            .GroupBy(r => r.StorageLocationId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        int Sum(Dictionary<Guid, int> a, Dictionary<Guid, int> b, Guid id)
            => a.GetValueOrDefault(id) + b.GetValueOrDefault(id);

        var categories = await _db.Categories.AsNoTracking()
            .Include(c => c.ItemTypes)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        var locations = await _db.CampusLocations.AsNoTracking().OrderBy(l => l.Name).ToListAsync(cancellationToken);
        var storage = await _db.StorageLocations.AsNoTracking().OrderBy(s => s.Name).ToListAsync(cancellationToken);

        return new ReferenceAdminDto(
            categories.Select(c => new CategoryAdminDto(
                c.Id,
                c.Name,
                c.Description,
                c.IsHighlighted,
                c.IsActive,
                Sum(lostByCategory, foundByCategory, c.Id),
                c.ItemTypes
                    .Where(t => !t.IsDeleted)
                    .OrderBy(t => t.Name)
                    .Select(t => new ItemTypeAdminDto(t.Id, t.CategoryId, t.Name, t.IsActive, Sum(lostByType, foundByType, t.Id)))
                    .ToList())).ToList(),
            locations.Select(l => new LocationAdminDto(
                l.Id, l.Name, l.Building, l.Description, l.IsActive, Sum(lostByPlace, foundByPlace, l.Id))).ToList(),
            storage.Select(s => new StorageAdminDto(
                s.Id, s.Name, s.Building, s.Capacity, s.IsActive, foundByStorage.GetValueOrDefault(s.Id))).ToList());
    }

    public async Task<Guid> CreateAsync(ReferenceKind kind, ReferenceItemRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();

        switch (kind)
        {
            case ReferenceKind.Categories:
            {
                var existing = await _db.Categories.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower(), cancellationToken);
                if (existing is not null && !existing.IsDeleted)
                    throw new ConflictAppException($"There is already a category called '{existing.Name}'.");

                var category = existing ?? new Category();
                category.Name = name;
                category.Description = Clean(request.Description);
                category.IsHighlighted = request.IsHighlighted ?? false;
                Undelete(category);
                category.IsActive = true;
                if (existing is null) _db.Categories.Add(category);
                await _db.SaveChangesAsync(cancellationToken);
                return category.Id;
            }

            case ReferenceKind.ItemTypes:
            {
                var categoryId = request.CategoryId
                    ?? throw new ValidationAppException(nameof(ReferenceItemRequest.CategoryId), "Say which category it belongs to.");
                if (!await _db.Categories.AnyAsync(c => c.Id == categoryId, cancellationToken))
                    throw new NotFoundAppException("That category does not exist.");

                var existing = await _db.ItemTypes.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(t => t.CategoryId == categoryId && t.Name.ToLower() == name.ToLower(), cancellationToken);
                if (existing is not null && !existing.IsDeleted)
                    throw new ConflictAppException($"That category already has '{existing.Name}'.");

                var type = existing ?? new ItemType { CategoryId = categoryId };
                type.Name = name;
                Undelete(type);
                type.IsActive = true;
                if (existing is null) _db.ItemTypes.Add(type);
                await _db.SaveChangesAsync(cancellationToken);
                return type.Id;
            }

            case ReferenceKind.Locations:
            {
                var building = Clean(request.Building);
                var existing = await _db.CampusLocations.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(l => l.Name.ToLower() == name.ToLower() && l.Building == building, cancellationToken);
                if (existing is not null && !existing.IsDeleted)
                    throw new ConflictAppException($"There is already a place called '{existing.Name}'.");

                var location = existing ?? new CampusLocation();
                location.Name = name;
                location.Building = building;
                location.Description = Clean(request.Description);
                Undelete(location);
                location.IsActive = true;
                if (existing is null) _db.CampusLocations.Add(location);
                await _db.SaveChangesAsync(cancellationToken);
                return location.Id;
            }

            case ReferenceKind.Storage:
            {
                var existing = await _db.StorageLocations.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.Name.ToLower() == name.ToLower(), cancellationToken);
                if (existing is not null && !existing.IsDeleted)
                    throw new ConflictAppException($"There is already storage called '{existing.Name}'.");

                var place = existing ?? new StorageLocation();
                place.Name = name;
                place.Building = Clean(request.Building);
                place.Capacity = request.Capacity;
                Undelete(place);
                place.IsActive = true;
                if (existing is null) _db.StorageLocations.Add(place);
                await _db.SaveChangesAsync(cancellationToken);
                return place.Id;
            }

            default:
                throw new ValidationAppException("kind", "Unknown kind of reference data.");
        }
    }

    public async Task UpdateAsync(ReferenceKind kind, Guid id, ReferenceItemRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();

        switch (kind)
        {
            case ReferenceKind.Categories:
            {
                var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                    ?? throw new NotFoundAppException("That category does not exist.");
                if (await _db.Categories.IgnoreQueryFilters().AnyAsync(c => c.Id != id && c.Name.ToLower() == name.ToLower(), cancellationToken))
                    throw new ConflictAppException("Another category already has that name.");
                category.Name = name;
                category.Description = Clean(request.Description);
                if (request.IsHighlighted is { } highlighted) category.IsHighlighted = highlighted;
                break;
            }

            case ReferenceKind.ItemTypes:
            {
                var type = await _db.ItemTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
                    ?? throw new NotFoundAppException("That item type does not exist.");
                if (await _db.ItemTypes.IgnoreQueryFilters().AnyAsync(
                        t => t.Id != id && t.CategoryId == type.CategoryId && t.Name.ToLower() == name.ToLower(), cancellationToken))
                    throw new ConflictAppException("That category already has an item type with that name.");
                type.Name = name;
                break;
            }

            case ReferenceKind.Locations:
            {
                var location = await _db.CampusLocations.FirstOrDefaultAsync(l => l.Id == id, cancellationToken)
                    ?? throw new NotFoundAppException("That place does not exist.");
                var building = Clean(request.Building);
                if (await _db.CampusLocations.IgnoreQueryFilters().AnyAsync(
                        l => l.Id != id && l.Name.ToLower() == name.ToLower() && l.Building == building, cancellationToken))
                    throw new ConflictAppException("Another place already has that name.");
                location.Name = name;
                location.Building = building;
                location.Description = Clean(request.Description);
                break;
            }

            case ReferenceKind.Storage:
            {
                var place = await _db.StorageLocations.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
                    ?? throw new NotFoundAppException("That storage does not exist.");
                if (await _db.StorageLocations.IgnoreQueryFilters().AnyAsync(s => s.Id != id && s.Name.ToLower() == name.ToLower(), cancellationToken))
                    throw new ConflictAppException("Other storage already has that name.");
                place.Name = name;
                place.Building = Clean(request.Building);
                place.Capacity = request.Capacity;
                break;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(ReferenceKind kind, Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        if (!isActive) await EnsureNotLastAvailableAsync(kind, id, cancellationToken);

        switch (kind)
        {
            case ReferenceKind.Categories:
                (await FindAsync(_db.Categories, id, "category", cancellationToken)).IsActive = isActive;
                break;
            case ReferenceKind.ItemTypes:
                (await FindAsync(_db.ItemTypes, id, "item type", cancellationToken)).IsActive = isActive;
                break;
            case ReferenceKind.Locations:
                (await FindAsync(_db.CampusLocations, id, "place", cancellationToken)).IsActive = isActive;
                break;
            case ReferenceKind.Storage:
                (await FindAsync(_db.StorageLocations, id, "storage", cancellationToken)).IsActive = isActive;
                break;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(ReferenceKind kind, Guid id, CancellationToken cancellationToken = default)
    {
        var usage = kind switch
        {
            ReferenceKind.Categories =>
                await _db.LostReports.IgnoreQueryFilters().CountAsync(r => r.CategoryId == id, cancellationToken)
                + await _db.FoundReports.IgnoreQueryFilters().CountAsync(r => r.CategoryId == id, cancellationToken),
            ReferenceKind.ItemTypes =>
                await _db.LostReports.IgnoreQueryFilters().CountAsync(r => r.ItemTypeId == id, cancellationToken)
                + await _db.FoundReports.IgnoreQueryFilters().CountAsync(r => r.ItemTypeId == id, cancellationToken),
            ReferenceKind.Locations =>
                await _db.LostReports.IgnoreQueryFilters().CountAsync(r => r.LastSeenLocationId == id, cancellationToken)
                + await _db.FoundReports.IgnoreQueryFilters().CountAsync(r => r.FoundLocationId == id, cancellationToken),
            ReferenceKind.Storage =>
                await _db.FoundReports.IgnoreQueryFilters().CountAsync(r => r.StorageLocationId == id, cancellationToken),
            _ => 0,
        };

        // Checked before the last-available guard: why it cannot go is the more useful answer.
        if (usage > 0)
        {
            throw new ConflictAppException(
                $"{usage} record{(usage == 1 ? " names" : "s name")} this, so it cannot be deleted. " +
                "Retire it instead: it leaves the pickers, and those records keep reading correctly.");
        }

        await EnsureNotLastAvailableAsync(kind, id, cancellationToken);

        switch (kind)
        {
            case ReferenceKind.Categories:
                var category = await _db.Categories.Include(c => c.ItemTypes).FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                    ?? throw new NotFoundAppException("That category does not exist.");
                // Its item types go with it - the category having no reports means none of them do.
                foreach (var type in category.ItemTypes) _db.ItemTypes.Remove(type);
                _db.Categories.Remove(category);
                break;
            case ReferenceKind.ItemTypes:
                _db.ItemTypes.Remove(await FindAsync(_db.ItemTypes, id, "item type", cancellationToken));
                break;
            case ReferenceKind.Locations:
                _db.CampusLocations.Remove(await FindAsync(_db.CampusLocations, id, "place", cancellationToken));
                break;
            case ReferenceKind.Storage:
                _db.StorageLocations.Remove(await FindAsync(_db.StorageLocations, id, "storage", cancellationToken));
                break;
        }

        // Remove() becomes a soft delete in FoundUDbContext.SaveChangesAsync.
        await _db.SaveChangesAsync(cancellationToken);
    }

    /* ------------------------------------------------------------------ internals */

    /// <summary>
    /// Retiring or deleting the last available category, place or storage would leave an empty
    /// picker - no way to report a loss, or to log an item at a desk.
    /// </summary>
    private async Task EnsureNotLastAvailableAsync(ReferenceKind kind, Guid id, CancellationToken cancellationToken)
    {
        var othersAvailable = kind switch
        {
            ReferenceKind.Categories => await _db.Categories.AnyAsync(c => c.Id != id && c.IsActive, cancellationToken),
            ReferenceKind.Locations => await _db.CampusLocations.AnyAsync(l => l.Id != id && l.IsActive, cancellationToken),
            ReferenceKind.Storage => await _db.StorageLocations.AnyAsync(s => s.Id != id && s.IsActive, cancellationToken),
            ReferenceKind.ItemTypes => await ItemTypeHasSiblingAsync(id, cancellationToken),
            _ => true,
        };

        if (!othersAvailable)
        {
            throw new ConflictAppException(kind switch
            {
                ReferenceKind.Categories => "This is the last category - add another before retiring it.",
                ReferenceKind.Locations => "This is the last campus place - add another before retiring it.",
                ReferenceKind.Storage => "This is the last storage - the desk would have nowhere to log items.",
                _ => "A category needs at least one item type - add another first.",
            });
        }
    }

    private async Task<bool> ItemTypeHasSiblingAsync(Guid id, CancellationToken cancellationToken)
    {
        var categoryId = await _db.ItemTypes.Where(t => t.Id == id).Select(t => (Guid?)t.CategoryId).FirstOrDefaultAsync(cancellationToken);
        return categoryId is null
            || await _db.ItemTypes.AnyAsync(t => t.CategoryId == categoryId && t.Id != id && t.IsActive, cancellationToken);
    }

    private static async Task<T> FindAsync<T>(DbSet<T> set, Guid id, string label, CancellationToken cancellationToken)
        where T : class
        => await set.FindAsync([id], cancellationToken) ?? throw new NotFoundAppException($"That {label} does not exist.");

    private static async Task<Dictionary<Guid, int>> CountBy<T>(
        IQueryable<T> source,
        System.Linq.Expressions.Expression<Func<T, Guid>> key,
        CancellationToken cancellationToken)
        => await source.GroupBy(key).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

    /// <summary>A name that was deleted comes back as it was, rather than failing on the unique index.</summary>
    private static void Undelete(ISoftDeletable entity)
    {
        entity.IsDeleted = false;
        entity.DeletedAt = null;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
