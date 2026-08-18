using FoundU.Application.Common.Pagination;

namespace FoundU.Application.LostReports.Dtos;

/// <summary>
/// Student-submitted lost item. The time window replaces a single "last seen" instant so the
/// Matching Agent can compare a found item's FoundAt against a range.
/// </summary>
public record CreateLostReportRequest(
    Guid CategoryId,
    Guid ItemTypeId,
    Guid LastSeenLocationId,
    string Description,
    string? PrimaryColor,
    string? SecondaryColor,
    DateTime EstimatedLostFromAt,
    DateTime EstimatedLostToAt);

public record LostReportListItemDto(
    Guid Id,
    string CategoryName,
    string ItemTypeName,
    string LastSeenLocationName,
    string Description,
    string? PrimaryColor,
    DateTime EstimatedLostFromAt,
    DateTime EstimatedLostToAt,
    string Status,
    DateTime CreatedAt);

public record LostReportDetailDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    Guid ItemTypeId,
    string ItemTypeName,
    Guid LastSeenLocationId,
    string LastSeenLocationName,
    string Description,
    string? PrimaryColor,
    string? SecondaryColor,
    DateTime EstimatedLostFromAt,
    DateTime EstimatedLostToAt,
    string Status,
    string? WithdrawReason,
    DateTime? WithdrawnAt,
    Guid StudentId,
    string StudentName,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record WithdrawLostReportRequest(string? Reason);

public class LostReportQuery : PaginationQuery
{
    /// <summary>Filter by LostReportStatus name (Active, Matched, Resolved, Withdrawn).</summary>
    public string? Status { get; set; }

    public Guid? CategoryId { get; set; }
    public Guid? ItemTypeId { get; set; }
    public Guid? LastSeenLocationId { get; set; }
}
