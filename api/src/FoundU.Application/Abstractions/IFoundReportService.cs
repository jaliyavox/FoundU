using FoundU.Application.Common.Pagination;
using FoundU.Application.FoundReports.Dtos;

namespace FoundU.Application.Abstractions;

/// <summary>
/// Found items logged by Staff/Security. Every method here is Staff/Admin territory - the
/// student-facing view of a found item arrives in Step 7 via match suggestions and claims,
/// and must use FoundReportSummaryDto so verification evidence stays hidden.
/// </summary>
public interface IFoundReportService
{
    Task<FoundReportDetailDto> CreateAsync(CreateFoundReportRequest request, Guid staffId, CancellationToken cancellationToken = default);
    Task<PagedResult<FoundReportListItemDto>> SearchAsync(FoundReportQuery query, CancellationToken cancellationToken = default);
    Task<FoundReportDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
