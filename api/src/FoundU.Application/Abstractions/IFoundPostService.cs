using FoundU.Application.Common.Pagination;
using FoundU.Application.FoundReports.Dtos;

namespace FoundU.Application.Abstractions;

/// <summary>
/// Finder-posted found items: the "I found something while walking" story.
///
/// A post is a FoundReport in the Posted state - no desk, no storage, no hidden detail. It
/// sits on the feed as a teaser so the owner can spot it and the matching agent has
/// something to match against, and it cannot be claimed until a desk confirms it. Confirming
/// is what turns it into the record every other part of the system already understands.
/// </summary>
public interface IFoundPostService
{
    Task<FoundPostFeedItemDto> PostAsync(CreateFoundPostRequest request, Guid finderId, CancellationToken cancellationToken = default);

    /// <summary>Public. Posted items only, newest first; a token marks the caller's own posts.</summary>
    Task<PagedResult<FoundPostFeedItemDto>> GetFeedAsync(FoundPostQuery query, Guid? requesterId, CancellationToken cancellationToken = default);

    /// <summary>The finder's own posts, in every state, so they can see what became of them.</summary>
    Task<PagedResult<FoundPostFeedItemDto>> GetMineAsync(Guid finderId, PaginationQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// An owner saying "that is mine" from the feed. Creates the suggestion on their report and
    /// tells the finder to hand it in - it does not prove anything; the desk still does that.
    /// </summary>
    Task<FoundPostFeedItemDto> RecogniseAsync(Guid id, Guid ownerId, RecogniseFoundPostRequest request, CancellationToken cancellationToken = default);

    /// <summary>The finder taking their post down before a desk sees it.</summary>
    Task<FoundPostFeedItemDto> WithdrawAsync(Guid id, Guid finderId, string? reason, CancellationToken cancellationToken = default);

    /// <summary>Staff pulling a post up by the code the finder quotes at the desk.</summary>
    Task<FoundReportDetailDto> GetByHandInCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// The desk confirming a post: it becomes Unclaimed, in storage, with the hidden detail
    /// written - from here on it is an ordinary found report.
    /// </summary>
    Task<FoundReportDetailDto> ConfirmAsync(Guid id, Guid staffId, ConfirmFoundPostRequest request, CancellationToken cancellationToken = default);
}
