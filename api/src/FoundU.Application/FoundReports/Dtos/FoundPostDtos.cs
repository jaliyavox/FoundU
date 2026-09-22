using FoundU.Application.Common.Pagination;

namespace FoundU.Application.FoundReports.Dtos;

/// <summary>
/// A student saying "I found something" before it reaches a desk.
///
/// Deliberately thin. The description is a teaser - enough for the owner to recognise their
/// own thing on the feed, not enough for a stranger to claim it. There is no hidden detail
/// here; the desk writes that when the item is handed in, and only then can it be claimed.
/// </summary>
public record CreateFoundPostRequest(
    Guid CategoryId,
    Guid ItemTypeId,
    Guid FoundLocationId,
    string Description,
    string? PrimaryColor,
    DateTime FoundAt,
    /// <summary>
    /// If the finder already spotted the matching lost post, its six digits. Links the two on
    /// the spot and tells the owner - no agent needed.
    /// </summary>
    string? LostReportHandInCode);

/// <summary>
/// A finder's post as the feed shows it. Finder's display name only, like the lost feed.
/// <c>HandInCode</c> is present only on your own post: it is what you quote at the desk.
/// </summary>
public record FoundPostFeedItemDto(
    Guid Id,
    string PostedByName,
    bool IsMine,
    string CategoryName,
    string ItemTypeName,
    string FoundLocationName,
    string Description,
    string? PrimaryColor,
    DateTime FoundAt,
    string Status,
    string? HandInCode,
    DateTime CreatedAt);

/// <summary>
/// The desk turning a post into a real record: where it is now, and the detail that was
/// never published, which is what a claimant will be asked about.
/// </summary>
public record ConfirmFoundPostRequest(
    Guid StorageLocationId,
    string? PrivateVerificationDetails,
    /// <summary>Optional rewrite of the finder's description in the desk's own words.</summary>
    string? GeneralDescription);

public record WithdrawFoundPostRequest(string? Reason);

public class FoundPostQuery : PaginationQuery
{
    public Guid? CategoryId { get; set; }
}

/// <summary>An owner recognising their item on the found feed, naming which of their reports it matches.</summary>
public record RecogniseFoundPostRequest(Guid LostReportId);
