namespace FoundU.Application.Handovers.Dtos;

/// <summary>
/// A handover in progress, as the two people involved see it.
///
/// <c>Code</c> is only ever filled for the finder who started it and for the report's owner.
/// It is not on the feed and not on any staff-facing payload - staff type it in, they do not
/// read it out.
/// </summary>
public record HandoverDto(
    Guid ReportId,
    string Status,
    string? Code,
    DateTime? StartedAt,
    DateTime? ExpiresAt,
    DateTime? HandedInAt,
    DateTime? CollectedAt,
    /// <summary>Where the desk put it, once a desk has it.</summary>
    string? StorageLocationName,
    string? FinderName);

/// <summary>What the desk sees when it types a code in. No code comes back out.</summary>
public record HandoverLookupDto(
    Guid ReportId,
    string Status,
    string ItemTypeName,
    string CategoryName,
    string Description,
    string? PrimaryColor,
    string LastSeenLocationName,
    string OwnerName,
    string? OwnerStudentNumber,
    string FinderName,
    DateTime StartedAt,
    DateTime? ExpiresAt,
    DateTime? HandedInAt,
    string? StorageLocationName,
    /// <summary>"receive" when the item is still with the finder, "release" when it is on a shelf.</summary>
    string NextStep);

/// <summary>The desk taking the item in. Where it goes on the shelf is the one thing it must record.</summary>
public record ReceiveHandoverRequest(Guid StorageLocationId, string? Note);

/// <summary>
/// The desk releasing the item to its owner. <c>OwnerIdChecked</c> is deliberately required:
/// a code says which item, never who the person is, and the check is what the record keeps.
/// </summary>
public record ReleaseHandoverRequest(bool OwnerIdChecked, string? Note);
