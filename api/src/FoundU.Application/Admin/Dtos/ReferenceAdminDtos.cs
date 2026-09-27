namespace FoundU.Application.Admin.Dtos;

/// <summary>
/// The four kinds of reference data an admin looks after. On the wire they are the route
/// segment: categories, item-types, locations, storage.
/// </summary>
public enum ReferenceKind
{
    Categories,
    ItemTypes,
    Locations,
    Storage
}

/// <summary>
/// One shape for creating or editing any of the four. The fields that do not apply to a kind
/// are ignored for it: Building for a category, CategoryId for a location, and so on.
/// </summary>
public record ReferenceItemRequest(
    string Name,
    string? Description,
    string? Building,
    int? Capacity,
    Guid? CategoryId,
    bool? IsHighlighted);

public record SetReferenceActiveRequest(bool IsActive);

/// <summary>
/// <c>UsageCount</c> is how many reports and items name this row. It decides what an admin may
/// do: anything in use can be retired - hidden from the pickers, still readable on the
/// records that already name it - but only something unused can be deleted.
/// </summary>
public record ItemTypeAdminDto(Guid Id, Guid CategoryId, string Name, bool IsActive, int UsageCount);

public record CategoryAdminDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsHighlighted,
    bool IsActive,
    int UsageCount,
    IReadOnlyList<ItemTypeAdminDto> ItemTypes);

public record LocationAdminDto(Guid Id, string Name, string? Building, string? Description, bool IsActive, int UsageCount);

public record StorageAdminDto(Guid Id, string Name, string? Building, int? Capacity, bool IsActive, int UsageCount);

public record ReferenceAdminDto(
    IReadOnlyList<CategoryAdminDto> Categories,
    IReadOnlyList<LocationAdminDto> Locations,
    IReadOnlyList<StorageAdminDto> Storage);
