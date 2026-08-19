using FoundU.Application.Common.Pagination;
using FoundU.Application.LostReports.Dtos;

namespace FoundU.Application.Abstractions;

/// <summary>
/// Lost items reported by students. Ownership matters here in a way it does not for found
/// reports: a student may only read or withdraw their own, which is why the caller's id and
/// whether they are staff are passed in rather than inferred.
/// </summary>
public interface ILostReportService
{
    Task<LostReportDetailDto> CreateAsync(CreateLostReportRequest request, Guid studentId, CancellationToken cancellationToken = default);

    /// <summary>Staff/Admin view across every student's reports.</summary>
    Task<PagedResult<LostReportListItemDto>> SearchAsync(LostReportQuery query, CancellationToken cancellationToken = default);

    /// <summary>The signed-in student's own reports.</summary>
    Task<PagedResult<LostReportListItemDto>> SearchForStudentAsync(Guid studentId, LostReportQuery query, CancellationToken cancellationToken = default);

    Task<LostReportDetailDto> GetByIdAsync(Guid id, Guid requesterId, bool requesterIsStaff, CancellationToken cancellationToken = default);

    /// <summary>
    /// Public, unauthenticated feed. Active reports only - withdrawn and resolved ones drop off,
    /// so the board reflects what people are still looking for.
    /// </summary>
    Task<PagedResult<LostReportFeedItemDto>> GetPublicFeedAsync(LostReportQuery query, CancellationToken cancellationToken = default);

    Task<LostReportDetailDto> WithdrawAsync(Guid id, Guid studentId, string? reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message to the report's author. Requires a signed-in sender, who may not be
    /// the author themselves, and only works while the report is still Active.
    /// </summary>
    Task<LostReportMessageDto> SendMessageAsync(Guid reportId, Guid senderId, string body, CancellationToken cancellationToken = default);

    /// <summary>The report author's messages. Staff may also read them for dispute handling.</summary>
    Task<IReadOnlyList<LostReportMessageDto>> GetMessagesAsync(Guid reportId, Guid requesterId, bool requesterIsStaff, CancellationToken cancellationToken = default);
}
