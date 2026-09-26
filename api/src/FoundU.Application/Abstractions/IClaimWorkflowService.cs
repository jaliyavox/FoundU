using FoundU.Application.Claims.Dtos;

namespace FoundU.Application.Abstractions;

/// <summary>Staff-only, claim-scoped gateway to a non-authoritative AI workflow approval.</summary>
public interface IClaimWorkflowService
{
    Task<AgentWorkflowStateDto> GetAsync(Guid claimId, Guid workflowId, CancellationToken cancellationToken = default);
    Task<AgentWorkflowStateDto> DecideAsync(Guid claimId, Guid workflowId, string decision, Guid decisionMakerId, CancellationToken cancellationToken = default);
}
