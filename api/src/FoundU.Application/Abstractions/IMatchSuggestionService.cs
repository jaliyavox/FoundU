using FoundU.Application.Common.Pagination;
using FoundU.Application.Matching.Dtos;

namespace FoundU.Application.Abstractions;

/// <summary>
/// The bridge between an item in storage and the student looking for it.
///
/// Found reports are staff-only on purpose - there is no browsable list of what has been
/// handed in, because that is how someone shops for a thing to claim. A suggestion is the
/// controlled way a student learns that one specific item might be theirs.
/// </summary>
public interface IMatchSuggestionService
{
    /// <summary>Staff linking an item to a report by hand.</summary>
    Task<MatchSuggestionDto> CreateAsync(CreateMatchSuggestionRequest request, Guid staffId, CancellationToken cancellationToken = default);

    /// <summary>Open suggestions across the student's own reports.</summary>
    Task<PagedResult<MatchSuggestionDto>> GetForStudentAsync(Guid studentId, PaginationQuery query, CancellationToken cancellationToken = default);

    /// <summary>Every suggestion on one found item, for the staff working it.</summary>
    Task<IReadOnlyList<MatchSuggestionDto>> GetForFoundReportAsync(Guid foundReportId, CancellationToken cancellationToken = default);

    /// <summary>The student saying "that is not mine", which takes it off their list.</summary>
    Task<MatchSuggestionDto> DismissAsync(Guid id, Guid studentId, string? reason, CancellationToken cancellationToken = default);
}
