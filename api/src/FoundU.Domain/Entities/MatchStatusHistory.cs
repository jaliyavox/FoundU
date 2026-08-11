using FoundU.Domain.Common;
using FoundU.Domain.Enums;

namespace FoundU.Domain.Entities;

public class MatchStatusHistory : BaseEntity
{
    public Guid MatchSuggestionId { get; set; }
    public MatchSuggestion MatchSuggestion { get; set; } = default!;

    public MatchSuggestionStatus FromStatus { get; set; }
    public MatchSuggestionStatus ToStatus { get; set; }

    public Guid? ChangedByUserId { get; set; }
    public AppUser? ChangedByUser { get; set; }

    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
