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

public record LostReportPhotoDto(Guid Id, string Url);

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
    IReadOnlyList<string> PhotoUrls,
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

/// <summary>
/// Anonymous public feed projection. Carries the poster's display name because the feed is a
/// community board, but deliberately nothing else identifying - no email, no student number,
/// no internal ids beyond the report itself.
/// </summary>
public record LostReportFeedItemDto(
    Guid Id,
    string PostedByName,
    string CategoryName,
    string ItemTypeName,
    string LastSeenLocationName,
    string Description,
    string? PrimaryColor,
    DateTime EstimatedLostFromAt,
    DateTime EstimatedLostToAt,
    IReadOnlyList<string> PhotoUrls,
    DateTime CreatedAt);

public class LostReportQuery : PaginationQuery
{
    /// <summary>Filter by LostReportStatus name (Active, Matched, Resolved, Withdrawn).</summary>
    public string? Status { get; set; }

    public Guid? CategoryId { get; set; }
    public Guid? ItemTypeId { get; set; }
    public Guid? LastSeenLocationId { get; set; }
}

/// <summary>Written by a signed-in user to the author of a lost report.</summary>
public record SendLostReportMessageRequest(string Body);

/// <summary>
/// A message as the report's author sees it. Carries the sender's display name only - never
/// their email or student number, the same rule the public feed follows.
/// </summary>
public record LostReportMessageDto(
    Guid Id,
    string SenderName,
    string Body,
    bool IsRead,
    DateTime CreatedAt);
