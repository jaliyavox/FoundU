using FoundU.Application.Honor.Dtos;

namespace FoundU.Application.Abstractions;

public interface IHelpToFindService
{
    /// <summary>Everything one person has done to help someone else get their things back.</summary>
    Task<HelpToFindDto> GetAsync(Guid userId, CancellationToken cancellationToken = default);
}
