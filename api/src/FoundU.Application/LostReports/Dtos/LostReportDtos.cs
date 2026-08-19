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
    /// <summary>
    /// How many people have written to the author about this report. A message here means
    /// "I found this" - it is the only thing anyone can send - so a non-zero count is the
    /// signal that the item has been found, before staff have logged it.
    /// </summary>
    int MessageCount,
    /// <summary>
    /// How many people have pressed "I found this". The author's card turns this into the
    /// "someone found it" checkpoint, whether or not the finder also wrote a message.
    /// </summary>
    int FoundClaimCount,
    /// <summary>When the most recent one came in, for "someone found this, 2 hours ago".</summary>
    DateTime? LastFoundClaimAt,
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
    /// <summary>
    /// True when the caller posted this report. Lets the feed hide "I found this" on your own
    /// post without the payload carrying the author's id, which would defeat the point of
    /// projecting a display name only.
    /// </summary>
    bool IsMine,
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

/// <summary>
/// The result of pressing "I found this". Deliberately thin: it confirms the signal was
/// recorded and tells the finder how many others have said the same, which is worth knowing
/// before they carry an item across campus.
/// </summary>
public record LostReportFoundClaimDto(Guid ReportId, int TotalFinders, DateTime CreatedAt);

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
