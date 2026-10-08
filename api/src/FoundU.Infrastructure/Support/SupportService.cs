using FoundU.Application.Abstractions;
using FoundU.Application.Common.Exceptions;
using FoundU.Application.Common.Pagination;
using FoundU.Application.Support.Dtos;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Infrastructure.Support;

/// <summary>
/// Support tickets: a student asking the people who run FoundU for help, and the desk
/// answering. One conversation per ticket, readable by the person who raised it and by staff.
///
/// Staff see the email address of whoever raised it, because answering sometimes happens
/// outside the app. Nothing else crosses: a ticket carries no verification detail and no
/// collection code, and naming a report in a ticket grants nobody access to it.
/// </summary>
public class SupportService : ISupportService
{
    private readonly FoundUDbContext _db;

    /// <summary>How long a resolved ticket still takes a follow-up from the assistant.</summary>
    private static readonly TimeSpan RecentlyResolved = TimeSpan.FromDays(7);
    private readonly INotificationService _notifications;

    public SupportService(FoundUDbContext db, INotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task<SupportTicketDetailDto> CreateAsync(
        CreateSupportTicketRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var category = Enum.Parse<SupportTicketCategory>(request.Category, ignoreCase: true);

        // The assistant escalates whatever the person is stuck on, and people come back to it
        // with the same problem. One conversation per problem: a live ticket on the same topic
        // - still open, waiting on them, or resolved this week - takes the new message and is
        // reopened, instead of a second ticket the desk has to match up by hand.
        if (request.ViaAssistant)
        {
            var reopenAfter = DateTime.UtcNow - RecentlyResolved;
            var existing = await _db.SupportTickets
                .Where(t => t.UserId == userId && t.Category == category
                    && (t.Status == SupportTicketStatus.Open
                        || t.Status == SupportTicketStatus.Waiting
                        || (t.Status == SupportTicketStatus.Resolved && t.ResolvedAt >= reopenAfter)))
                .OrderByDescending(t => t.LastActivityAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing is not null)
            {
                var subject = request.Subject.Trim();
                var body = request.Body.Trim();
                _db.SupportTicketMessages.Add(new SupportTicketMessage
                {
                    SupportTicketId = existing.Id,
                    SenderId = userId,
                    IsStaffReply = false,
                    // The new subject may say what changed; keep it with the message when it fits
                    // the 4,000-character column.
                    Body = string.Equals(subject, existing.Subject, StringComparison.OrdinalIgnoreCase)
                           || subject.Length + 2 + body.Length > 4000
                        ? body
                        : $"{subject}\n\n{body}",
                });
                existing.Status = SupportTicketStatus.Open;
                existing.ResolvedAt = null;
                existing.LastActivityAt = DateTime.UtcNow;
                existing.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync(cancellationToken);
                return await LoadDetailAsync(existing.Id, userId, isStaff: false, cancellationToken) with { AddedToExisting = true };
            }
        }

        var ticket = new SupportTicket
        {
            UserId = userId,
            Subject = request.Subject.Trim(),
            Category = category,
            Status = SupportTicketStatus.Open,
            RelatedEntityType = string.IsNullOrWhiteSpace(request.RelatedEntityType) ? null : request.RelatedEntityType.Trim(),
            RelatedEntityId = request.RelatedEntityId,
            ViaAssistant = request.ViaAssistant,
            LastActivityAt = DateTime.UtcNow,
        };
        _db.SupportTickets.Add(ticket);

        ticket.Messages.Add(new SupportTicketMessage
        {
            SupportTicket = ticket,
            SenderId = userId,
            IsStaffReply = false,
            Body = request.Body.Trim(),
        });

        await _db.SaveChangesAsync(cancellationToken);
        return await LoadDetailAsync(ticket.Id, userId, isStaff: false, cancellationToken);
    }

    public Task<PagedResult<SupportTicketListItemDto>> GetMineAsync(
        Guid userId,
        SupportTicketQuery query,
        CancellationToken cancellationToken = default)
        => SearchCoreAsync(_db.SupportTickets.Where(t => t.UserId == userId), userId, isStaff: false, query, cancellationToken);

    public Task<PagedResult<SupportTicketListItemDto>> SearchAsync(
        Guid staffId,
        SupportTicketQuery query,
        CancellationToken cancellationToken = default)
    {
        var tickets = _db.SupportTickets.AsQueryable();

        if (string.Equals(query.Assigned, "mine", StringComparison.OrdinalIgnoreCase))
            tickets = tickets.Where(t => t.AssignedToUserId == staffId);
        else if (string.Equals(query.Assigned, "none", StringComparison.OrdinalIgnoreCase))
            tickets = tickets.Where(t => t.AssignedToUserId == null);

        return SearchCoreAsync(tickets, staffId, isStaff: true, query, cancellationToken);
    }

    public async Task<SupportTicketDetailDto> GetByIdAsync(
        Guid id,
        Guid userId,
        bool isStaff,
        CancellationToken cancellationToken = default)
    {
        await MarkReadAsync(id, userId, isStaff, cancellationToken);
        return await LoadDetailAsync(id, userId, isStaff, cancellationToken);
    }

    public async Task<SupportTicketDetailDto> ReplyAsync(
        Guid id,
        Guid userId,
        bool isStaff,
        string body,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _db.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundAppException($"Ticket '{id}' was not found.");

        if (!isStaff && ticket.UserId != userId)
            throw new ForbiddenAppException("You can only write on your own tickets.");

        if (ticket.Status == SupportTicketStatus.Closed)
            throw new ConflictAppException("This ticket is closed. Open a new one and we will pick it up there.");

        // Staff writing as staff, even on a ticket they happen to have raised themselves, is
        // recorded from how they are acting here rather than from their role, which can change.
        var staffReply = isStaff && ticket.UserId != userId;

        _db.SupportTicketMessages.Add(new SupportTicketMessage
        {
            SupportTicketId = ticket.Id,
            SenderId = userId,
            IsStaffReply = staffReply,
            Body = body.Trim(),
        });

        // A reply from the desk waits on the person; a reply from the person reopens the queue.
        ticket.Status = staffReply ? SupportTicketStatus.Waiting : SupportTicketStatus.Open;
        if (staffReply && ticket.AssignedToUserId is null) ticket.AssignedToUserId = userId;
        ticket.LastActivityAt = DateTime.UtcNow;
        ticket.UpdatedAt = DateTime.UtcNow;
        if (ticket.Status != SupportTicketStatus.Resolved) ticket.ResolvedAt = null;

        if (staffReply)
        {
            _notifications.Queue(
                ticket.UserId,
                NotificationType.SupportTicketReply,
                "FoundU replied to your ticket",
                body.Length <= 140 ? body : body[..140] + "...",
                nameof(SupportTicket),
                ticket.Id);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await LoadDetailAsync(ticket.Id, userId, isStaff, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ticket = await _db.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundAppException($"Ticket '{id}' was not found.");

        // FoundUDbContext turns this into a soft delete (SupportTicket is ISoftDeletable).
        _db.SupportTickets.Remove(ticket);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SupportTicketDetailDto> UpdateAsync(
        Guid id,
        Guid staffId,
        UpdateSupportTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _db.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundAppException($"Ticket '{id}' was not found.");

        var status = Enum.Parse<SupportTicketStatus>(request.Status, ignoreCase: true);
        var statusChanged = ticket.Status != status;

        if (request.AssignedToUserId is { } assignee)
        {
            var isStaffMember = await _db.Users.AnyAsync(
                u => u.Id == assignee && (u.Role == UserRole.Staff || u.Role == UserRole.Admin),
                cancellationToken);
            if (!isStaffMember)
                throw new ValidationAppException(nameof(UpdateSupportTicketRequest.AssignedToUserId), "Tickets can only be assigned to staff.");
        }

        ticket.Status = status;
        ticket.AssignedToUserId = request.AssignedToUserId;
        ticket.ResolvedAt = status is SupportTicketStatus.Resolved or SupportTicketStatus.Closed
            ? ticket.ResolvedAt ?? DateTime.UtcNow
            : null;
        ticket.UpdatedAt = DateTime.UtcNow;

        // Silence on a ticket that was closed is worse than a notification nobody needed.
        if (statusChanged && status is SupportTicketStatus.Resolved or SupportTicketStatus.Closed)
        {
            _notifications.Queue(
                ticket.UserId,
                NotificationType.SupportTicketUpdated,
                status == SupportTicketStatus.Resolved ? "Your ticket was marked resolved" : "Your ticket was closed",
                status == SupportTicketStatus.Resolved
                    ? "If that did not settle it, write on the ticket and it comes straight back to us."
                    : "Open a new ticket if you need anything else.",
                nameof(SupportTicket),
                ticket.Id);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await LoadDetailAsync(ticket.Id, staffId, isStaff: true, cancellationToken);
    }

    public async Task<SupportQueueStatsDto> GetQueueStatsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;

        var open = await _db.SupportTickets.CountAsync(t => t.Status == SupportTicketStatus.Open, cancellationToken);
        var waiting = await _db.SupportTickets.CountAsync(t => t.Status == SupportTicketStatus.Waiting, cancellationToken);
        var resolvedToday = await _db.SupportTickets.CountAsync(
            t => t.ResolvedAt != null && t.ResolvedAt >= today, cancellationToken);
        var unassigned = await _db.SupportTickets.CountAsync(
            t => t.AssignedToUserId == null && t.Status == SupportTicketStatus.Open, cancellationToken);

        var oldest = await _db.SupportTickets
            .Where(t => t.Status == SupportTicketStatus.Open)
            .OrderBy(t => t.LastActivityAt)
            .Select(t => (DateTime?)t.LastActivityAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new SupportQueueStatsDto(
            open,
            waiting,
            resolvedToday,
            unassigned,
            oldest is null ? 0 : (int)Math.Max(0, (DateTime.UtcNow - oldest.Value).TotalHours));
    }

    /* ------------------------------------------------------------------ internals */

    private async Task<PagedResult<SupportTicketListItemDto>> SearchCoreAsync(
        IQueryable<SupportTicket> tickets,
        Guid readerId,
        bool isStaff,
        SupportTicketQuery query,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<SupportTicketStatus>(query.Status, ignoreCase: true, out var status) && Enum.IsDefined(status))
        {
            tickets = tickets.Where(t => t.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Category)
            && Enum.TryParse<SupportTicketCategory>(query.Category, ignoreCase: true, out var category) && Enum.IsDefined(category))
        {
            tickets = tickets.Where(t => t.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            tickets = tickets.Where(t =>
                t.Subject.ToLower().Contains(term)
                || t.Messages.Any(m => m.Body.ToLower().Contains(term)));
        }

        var total = await tickets.CountAsync(cancellationToken);

        // Longest wait first for the desk; most recent first for the person who raised it.
        tickets = isStaff
            ? tickets.OrderBy(t => t.Status == SupportTicketStatus.Open ? 0 : 1).ThenBy(t => t.LastActivityAt)
            : tickets.OrderByDescending(t => t.LastActivityAt);

        var items = await tickets
            .AsNoTracking()
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(t => new SupportTicketListItemDto(
                t.Id,
                t.Subject,
                t.Category.ToString(),
                t.Status.ToString(),
                t.User.FullName,
                t.AssignedToUser == null ? null : t.AssignedToUser.FullName,
                t.Messages.Count,
                // Unread means "written by the other side and not yet seen by this reader".
                t.Messages.Count(m => !m.IsRead && (isStaff ? !m.IsStaffReply : m.IsStaffReply)),
                t.LastActivityAt,
                t.CreatedAt,
                t.ViaAssistant))
            .ToListAsync(cancellationToken);

        return PagedResult<SupportTicketListItemDto>.Create(items, query.Page, query.PageSize, total);
    }

    private async Task MarkReadAsync(Guid id, Guid userId, bool isStaff, CancellationToken cancellationToken)
    {
        var ticket = await _db.SupportTickets
            .AsNoTracking()
            .Select(t => new { t.Id, t.UserId })
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundAppException($"Ticket '{id}' was not found.");

        if (!isStaff && ticket.UserId != userId)
            throw new ForbiddenAppException("You can only read your own tickets.");

        var unread = await _db.SupportTicketMessages
            .Where(m => m.SupportTicketId == id && !m.IsRead && (isStaff ? !m.IsStaffReply : m.IsStaffReply))
            .ToListAsync(cancellationToken);

        if (unread.Count == 0) return;

        foreach (var message in unread) message.IsRead = true;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<SupportTicketDetailDto> LoadDetailAsync(
        Guid id,
        Guid readerId,
        bool isStaff,
        CancellationToken cancellationToken)
    {
        var ticket = await _db.SupportTickets
            .AsNoTracking()
            .Include(t => t.User)
            .Include(t => t.AssignedToUser)
            .Include(t => t.Messages.OrderBy(m => m.CreatedAt))
                .ThenInclude(m => m.Sender)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundAppException($"Ticket '{id}' was not found.");

        if (!isStaff && ticket.UserId != readerId)
            throw new ForbiddenAppException("You can only read your own tickets.");

        return new SupportTicketDetailDto(
            ticket.Id,
            ticket.Subject,
            ticket.Category.ToString(),
            ticket.Status.ToString(),
            ticket.UserId,
            ticket.User.FullName,
            // Staff may need to reach the person outside the app; the person does not need
            // their own address read back to them from here.
            isStaff ? ticket.User.Email : null,
            ticket.AssignedToUserId,
            ticket.AssignedToUser?.FullName,
            ticket.RelatedEntityType,
            ticket.RelatedEntityId,
            ticket.LastActivityAt,
            ticket.ResolvedAt,
            ticket.CreatedAt,
            ticket.Messages.Select(m => new SupportTicketMessageDto(
                m.Id,
                m.Sender.FullName,
                m.SenderId == readerId,
                m.IsStaffReply,
                m.Body,
                m.CreatedAt)).ToList(),
            ticket.ViaAssistant);
    }
}
