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

    Task<LostReportDetailDto> WithdrawAsync(Guid id, Guid studentId, string? reason, CancellationToken cancellationToken = default);
}
