using FoundU.Application.Admin.Dtos;

namespace FoundU.Application.Abstractions;

/// <summary>Admin-only aggregates. Every figure comes from a count or a mean over real rows.</summary>
public interface IAdminAnalyticsService
{
    Task<AnalyticsOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
}
