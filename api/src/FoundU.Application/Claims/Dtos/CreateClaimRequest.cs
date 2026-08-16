namespace FoundU.Application.Claims.Dtos;

public record CreateClaimRequest(
    Guid LostReportId,
    Guid FoundReportId);
