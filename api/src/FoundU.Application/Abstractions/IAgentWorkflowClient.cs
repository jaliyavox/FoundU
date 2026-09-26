using FoundU.Application.Claims.Dtos;

namespace FoundU.Application.Abstractions;

/// <summary>Internal, service-key authenticated client for safe AI workflow lifecycle commands.</summary>
public interface IAgentWorkflowClient
{
    Task<AgentWorkflowStateDto?> StartCoordinatorAsync(Guid workflowId, string claimStatus, string verificationRecommendation, CancellationToken cancellationToken = default);
    Task<AgentWorkflowStateDto?> GetAsync(Guid workflowId, CancellationToken cancellationToken = default);
    Task<AgentWorkflowStateDto?> DecideAsync(Guid workflowId, string decision, Guid decisionMakerId, CancellationToken cancellationToken = default);
    Task<AgentWorkflowStateDto?> ResumeAsync(Guid workflowId, CancellationToken cancellationToken = default);
}
