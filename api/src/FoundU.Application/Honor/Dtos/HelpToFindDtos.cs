namespace FoundU.Application.Honor.Dtos;

/// <summary>
/// One thing the person did to help. Kind is "found-claim" (they said they found someone's
/// lost item) or "found-post" (they posted something they picked up).
///
/// <c>HandInCode</c> is the six digits to quote at the desk - it is the code from the owner's
/// own public report, or their own post's code, so nothing private travels here. It is null
/// once the item has reached a desk and the code has done its job.
/// </summary>
public record HelpToFindActivityDto(
    string Kind,
    Guid ReportId,
    string ItemTypeName,
    string LocationName,
    string Status,
    string? HandInCode,
    string? OwnerName,
    int PointsEarned,
    DateTime CreatedAt);

/// <summary>The "Help to find" page: what they have earned and everything they did to earn it.</summary>
public record HelpToFindDto(
    int HonorPoints,
    int ItemsReturned,
    int HandIns,
    int OpenHelpOffers,
    IReadOnlyList<HelpToFindActivityDto> Activity);
