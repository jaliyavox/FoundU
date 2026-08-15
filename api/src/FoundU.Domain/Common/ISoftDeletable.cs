namespace FoundU.Domain.Common;

/// <summary>
/// Implemented by entities that support soft delete instead of physical deletion.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}
