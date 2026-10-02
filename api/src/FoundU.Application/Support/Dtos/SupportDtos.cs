using FoundU.Application.Common.Pagination;

namespace FoundU.Application.Support.Dtos;

/// <summary>Opening a ticket. The first message is the body - there is no empty ticket.</summary>
public record CreateSupportTicketRequest(
    string Subject,
    string Category,
    string Body,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    /// <summary>Sent from the assistant's draft. A label for the desk, not a privilege.</summary>
    bool ViaAssistant = false);

public record SupportTicketReplyRequest(string Body);

/// <summary>Staff moving a ticket along. Assignment is optional - an unassigned desk still works.</summary>
public record UpdateSupportTicketRequest(string Status, Guid? AssignedToUserId);

public record SupportTicketMessageDto(
    Guid Id,
    string SenderName,
    bool IsMine,
    bool IsStaffReply,
    string Body,
    DateTime CreatedAt);

/// <summary>
/// A row in either queue - the person's own list, or the desk's. <c>UnreadCount</c> is what
/// the reader has not seen, which is what each side's attention should follow.
/// </summary>
public record SupportTicketListItemDto(
    Guid Id,
    string Subject,
    string Category,
    string Status,
    string RaisedByName,
    string? AssignedToName,
    int MessageCount,
    int UnreadCount,
    DateTime LastActivityAt,
    DateTime CreatedAt,
    bool ViaAssistant = false);

public record SupportTicketDetailDto(
    Guid Id,
    string Subject,
    string Category,
    string Status,
    Guid RaisedById,
    string RaisedByName,
    string? RaisedByEmail,
    Guid? AssignedToUserId,
    string? AssignedToName,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    DateTime LastActivityAt,
    DateTime? ResolvedAt,
    DateTime CreatedAt,
    IReadOnlyList<SupportTicketMessageDto> Messages,
    bool ViaAssistant = false);

public class SupportTicketQuery : PaginationQuery
{
    public string? Status { get; set; }
    public string? Category { get; set; }

    /// <summary>"mine" limits a staff queue to tickets assigned to the caller; "none" to unassigned.</summary>
    public string? Assigned { get; set; }
}

/// <summary>The numbers the admin overview leads with.</summary>
public record SupportQueueStatsDto(int Open, int Waiting, int ResolvedToday, int Unassigned, int OldestOpenHours);
