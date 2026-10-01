using FoundU.Application.Common.Pagination;
using FoundU.Application.Support.Dtos;

namespace FoundU.Application.Abstractions;

public interface ISupportService
{
    /// <summary>Opens a ticket in the caller's name, with their first message on it.</summary>
    Task<SupportTicketDetailDto> CreateAsync(CreateSupportTicketRequest request, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The caller's own tickets.</summary>
    Task<PagedResult<SupportTicketListItemDto>> GetMineAsync(Guid userId, SupportTicketQuery query, CancellationToken cancellationToken = default);

    /// <summary>The desk queue. Staff only - the caller has already been checked.</summary>
    Task<PagedResult<SupportTicketListItemDto>> SearchAsync(Guid staffId, SupportTicketQuery query, CancellationToken cancellationToken = default);

    /// <summary>One ticket, for the person who raised it or for staff. Reading it marks it read.</summary>
    Task<SupportTicketDetailDto> GetByIdAsync(Guid id, Guid userId, bool isStaff, CancellationToken cancellationToken = default);

    Task<SupportTicketDetailDto> ReplyAsync(Guid id, Guid userId, bool isStaff, string body, CancellationToken cancellationToken = default);

    /// <summary>Status and assignment. Staff only.</summary>
    Task<SupportTicketDetailDto> UpdateAsync(Guid id, Guid staffId, UpdateSupportTicketRequest request, CancellationToken cancellationToken = default);

    Task<SupportQueueStatsDto> GetQueueStatsAsync(CancellationToken cancellationToken = default);
}
