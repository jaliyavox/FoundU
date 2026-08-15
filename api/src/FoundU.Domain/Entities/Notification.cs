using FoundU.Domain.Common;
using FoundU.Domain.Enums;

namespace FoundU.Domain.Entities;

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = default!;

    public NotificationType Type { get; set; }
    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
}
