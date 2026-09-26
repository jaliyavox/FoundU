using FoundU.Application.Common.Pagination;

namespace FoundU.Application.FoundReports.Dtos;

/// <summary>
/// Staff-entered found item. Only Staff/Admin reach these endpoints, so PrivateVerificationDetails
/// is safe here - but note it is deliberately absent from FoundReportSummaryDto, which is the
/// shape any future student-facing surface (match suggestions, claim screens) must use.
/// </summary>
public record CreateFoundReportRequest(
    Guid CategoryId,
    Guid ItemTypeId,
    Guid FoundLocationId,
    Guid StorageLocationId,
    string GeneralDescription,
    string? PrivateVerificationDetails,
    string? PrimaryColor,
    string? SecondaryColor,
    DateTime FoundAt,
    /// <summary>
    /// The six digits the finder quoted from the lost report. When present the item is linked
    /// to that report the moment it is logged, and the owner is told - no searching.
    /// </summary>
    string? HandInCode = null);

/// <summary>Row in the staff items table. Keeps the payload small for list views.</summary>
public record FoundReportListItemDto(
    Guid Id,
    string CategoryName,
    string ItemTypeName,
    string FoundLocationName,
    string? StorageLocationName,
    string GeneralDescription,
    string? PrimaryColor,
    DateTime FoundAt,
    string Status,
    bool HasVerificationDetails,
    /// <summary>Set when a student posted it. Staff read a post differently from a desk record.</summary>
    string? FinderName,
    DateTime CreatedAt);

/// <summary>Full detail, Staff/Admin only - includes the hidden ownership evidence.</summary>
public record FoundReportDetailDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    Guid ItemTypeId,
    string ItemTypeName,
    Guid FoundLocationId,
    string FoundLocationName,
    /// <summary>Null while the item is only a finder's post - nothing is in storage yet.</summary>
    Guid? StorageLocationId,
    string? StorageLocationName,
    string GeneralDescription,
    string? PrivateVerificationDetails,
    string? PrimaryColor,
    string? SecondaryColor,
    DateTime FoundAt,
    string Status,
    Guid? StaffId,
    string? StaffName,
    /// <summary>The student who posted it, when it started as a finder's post.</summary>
    string? FinderName,
    /// <summary>The finder's code, for the desk to pull the post up. Staff only, and only on posts.</summary>
    string? HandInCode,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// Student-safe projection. Contains no verification evidence, so it can be handed to a
/// claimant without leaking the answers they are supposed to prove they know.
/// </summary>
public record FoundReportSummaryDto(
    Guid Id,
    string CategoryName,
    string ItemTypeName,
    string FoundLocationName,
    string GeneralDescription,
    string? PrimaryColor,
    DateTime FoundAt,
    string Status);

/// <summary>Filters for the staff items table, on top of the standard pagination contract.</summary>
public class FoundReportQuery : PaginationQuery
{
    /// <summary>Filter by FoundReportStatus name (Unclaimed, Claimed, Returned, Disposed).</summary>
    public string? Status { get; set; }

    public Guid? CategoryId { get; set; }
    public Guid? ItemTypeId { get; set; }
    public Guid? FoundLocationId { get; set; }

    /// <summary>Only items found on or after this instant.</summary>
    public DateTime? FoundFrom { get; set; }

    /// <summary>Only items found on or before this instant.</summary>
    public DateTime? FoundTo { get; set; }
}
