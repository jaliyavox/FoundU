using FoundU.Application.Reference.Dtos;

namespace FoundU.Application.Abstractions;

/// <summary>
/// Read-only taxonomy the report forms depend on: categories, item types, campus locations and
/// storage locations. Seeded through EF HasData - see the Persistence/Configurations classes.
/// </summary>
public interface IReferenceDataService
{
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CampusLocationDto>> GetCampusLocationsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StorageLocationDto>> GetStorageLocationsAsync(CancellationToken cancellationToken = default);
}
