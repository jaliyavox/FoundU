namespace FoundU.Application.Claims.Dtos;

public record ClaimResponse(
    Guid ClaimId,
    Guid LostReportId,
    Guid FoundReportId,
    string Status,
    DateTime CreatedAt);
