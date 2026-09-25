namespace FoundU.Application.Reference.Dtos;

/// <summary>A category with its item types nested, so a form can populate both dropdowns in one call.</summary>
public record CategoryDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsHighlighted,
    IReadOnlyList<ItemTypeDto> ItemTypes);

public record ItemTypeDto(Guid Id, Guid CategoryId, string Name);

public record CampusLocationDto(Guid Id, string Name, string? Building, string? Description);

/// <summary>Where a found item is physically held. Staff/Admin only - students never choose one.</summary>
public record StorageLocationDto(Guid Id, string Name, string? Building, int? Capacity);
