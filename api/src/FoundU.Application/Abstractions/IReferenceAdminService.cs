using FoundU.Application.Admin.Dtos;

namespace FoundU.Application.Abstractions;

/// <summary>
/// Categories, item types, campus locations and storage for admins: add, rename, retire,
/// restore, and delete what nothing uses. Everything a student or the desk picks from.
/// </summary>
public interface IReferenceAdminService
{
    /// <summary>Everything, retired rows included, with how much each is used.</summary>
    Task<ReferenceAdminDto> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(ReferenceKind kind, ReferenceItemRequest request, CancellationToken cancellationToken = default);

    Task UpdateAsync(ReferenceKind kind, Guid id, ReferenceItemRequest request, CancellationToken cancellationToken = default);

    /// <summary>Retire (hide from the pickers) or restore. Records that already name it are untouched.</summary>
    Task SetActiveAsync(ReferenceKind kind, Guid id, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>Only for something nothing uses; anything in use is retired instead.</summary>
    Task DeleteAsync(ReferenceKind kind, Guid id, CancellationToken cancellationToken = default);
}
