using FoundU.Application.Admin.Dtos;

namespace FoundU.Application.Abstractions;

public interface IAdminOverviewService
{
    Task<AdminOverviewDto> GetAsync(CancellationToken cancellationToken = default);
}
