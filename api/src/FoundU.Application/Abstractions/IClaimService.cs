using FoundU.Application.Claims.Dtos;

namespace FoundU.Application.Abstractions;

public interface IClaimService
{
    Task<ClaimResponse> CreateAsync(Guid authenticatedUserId, CreateClaimRequest request);
}
