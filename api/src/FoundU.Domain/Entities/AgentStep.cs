using FoundU.Domain.Common;
using FoundU.Domain.Enums;

namespace FoundU.Domain.Entities;

/// <summary>One delegated step executed within an AgentRun by a specific named agent.</summary>
public class AgentStep : BaseEntity
{
    public Guid AgentRunId { get; set; }
    public AgentRun AgentRun { get; set; } = default!;

    public AgentName AgentName { get; set; }
    public int StepOrder { get; set; }
    public string Task { get; set; } = default!;

    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }

    public AgentStepStatus Status { get; set; } = AgentStepStatus.Pending;
    public string? ErrorMessage { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
