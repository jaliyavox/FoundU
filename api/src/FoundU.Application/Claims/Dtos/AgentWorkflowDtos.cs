namespace FoundU.Application.Claims.Dtos;

/// <summary>Safe projection of durable AI workflow state. It deliberately has no prompt, evidence, or reasoning fields.</summary>
public record AgentWorkflowStateDto(
    Guid WorkflowId,
    string Status,
    bool ApprovalRequired,
    string ApprovalStatus,
    string? PendingActionType,
    string? SafeActionSummary,
    DateTimeOffset? RequestedAt,
    DateTimeOffset? DecidedAt,
    string? DecisionMakerId);

/// <summary>Only the decision is client supplied; the actor always comes from the staff JWT.</summary>
public record AgentWorkflowDecisionRequest(string Decision);
