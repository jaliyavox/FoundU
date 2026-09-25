using FoundU.Application.Common.Pagination;

namespace FoundU.Application.Notifications.Dtos;

/// <summary>
/// One thing that happened, addressed to one person.
///
/// Carries where to go next rather than making the client guess: <c>RelatedEntityType</c> and
/// <c>RelatedEntityId</c> are what the UI turns into a link.
/// </summary>
public record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string Message,
    bool IsRead,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    DateTime CreatedAt);

public record UnreadCountDto(int Unread);

public class NotificationQuery : PaginationQuery
{
    /// <summary>Only the ones still unread - what a bell badge opens onto.</summary>
    public bool? UnreadOnly { get; set; }
}
