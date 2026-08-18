using FoundU.Application.Abstractions;
using FoundU.Application.Reference.Dtos;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Infrastructure.Reporting;

public class ReferenceDataService : IReferenceDataService
{
    private readonly FoundUDbContext _db;

    public ReferenceDataService(FoundUDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        // Soft-deleted rows are excluded automatically by the global query filters.
        // Inactive rows stay hidden from the forms but remain valid on historic reports.
        return await _db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Description,
                c.ItemTypes
                    .Where(t => t.IsActive)
                    .OrderBy(t => t.Name)
                    .Select(t => new ItemTypeDto(t.Id, t.CategoryId, t.Name))
                    .ToList()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CampusLocationDto>> GetCampusLocationsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.CampusLocations
            .AsNoTracking()
            .Where(l => l.IsActive)
            .OrderBy(l => l.Name)
            .Select(l => new CampusLocationDto(l.Id, l.Name, l.Building, l.Description))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StorageLocationDto>> GetStorageLocationsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.StorageLocations
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new StorageLocationDto(s.Id, s.Name, s.Building, s.Capacity))
            .ToListAsync(cancellationToken);
    }
}
