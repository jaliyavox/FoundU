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
    DateTime FoundAt);

/// <summary>Row in the staff items table. Keeps the payload small for list views.</summary>
public record FoundReportListItemDto(
    Guid Id,
    string CategoryName,
    string ItemTypeName,
    string FoundLocationName,
    string StorageLocationName,
    string GeneralDescription,
    string? PrimaryColor,
    DateTime FoundAt,
    string Status,
    bool HasVerificationDetails,
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
    Guid StorageLocationId,
    string StorageLocationName,
    string GeneralDescription,
    string? PrivateVerificationDetails,
    string? PrimaryColor,
    string? SecondaryColor,
    DateTime FoundAt,
    string Status,
    Guid StaffId,
    string StaffName,
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
