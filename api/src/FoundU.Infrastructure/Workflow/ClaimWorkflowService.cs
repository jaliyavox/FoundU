using System.Text.Json;
using FoundU.Application.Abstractions;
using FoundU.Application.Claims.Dtos;
using FoundU.Application.Common.Exceptions;
using FoundU.Domain.Enums;
using FoundU.Domain.Entities;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Infrastructure.Workflow;

/// <summary>Ensures staff can operate only a coordinator workflow already recorded against this claim.</summary>
public sealed class ClaimWorkflowService : IClaimWorkflowService
{
    private readonly FoundUDbContext _db;
    private readonly IAgentWorkflowClient _workflows;

    public ClaimWorkflowService(FoundUDbContext db, IAgentWorkflowClient workflows)
    {
        _db = db;
        _workflows = workflows;
    }

    public async Task<AgentWorkflowStateDto> GetAsync(Guid claimId, Guid workflowId, CancellationToken cancellationToken = default)
    {
        await EnsureLinkedAsync(claimId, workflowId, cancellationToken);
        return await _workflows.GetAsync(workflowId, cancellationToken)
            ?? throw new NotFoundAppException("AI workflow was not found.");
    }

    public async Task<AgentWorkflowStateDto> DecideAsync(Guid claimId, Guid workflowId, string decision, Guid decisionMakerId, CancellationToken cancellationToken = default)
    {
        if (decision is not ("approved" or "rejected")) throw new ConflictAppException("Workflow decision must be approved or rejected.");
        await EnsureLinkedAsync(claimId, workflowId, cancellationToken);
        var current = await _workflows.GetAsync(workflowId, cancellationToken)
            ?? throw new NotFoundAppException("AI workflow was not found.");
        if (current.Status != "waiting_for_approval" || !current.ApprovalRequired)
            throw new ConflictAppException("AI workflow is not awaiting approval.");
        var decided = await _workflows.DecideAsync(workflowId, decision, decisionMakerId, cancellationToken)
            ?? throw new NotFoundAppException("AI workflow was not found.");
        // An approval continues safe coordination only. It never calls ClaimService.DecideAsync.
        var result = decision == "approved"
            ? await _workflows.ResumeAsync(workflowId, cancellationToken) ?? throw new NotFoundAppException("AI workflow was not found.")
            : decided;
        await SynchronizeRunAsync(claimId, workflowId, result.Status, cancellationToken);
        return result;
    }

    private async Task EnsureLinkedAsync(Guid claimId, Guid workflowId, CancellationToken cancellationToken)
    {
        var linkedOutcomes = await _db.AgentRuns.AsNoTracking().Where(run => run.ClaimId == claimId)
            .Select(run => run.FinalOutcomeJson).ToListAsync(cancellationToken);
        if (!linkedOutcomes.Any(json => ContainsWorkflowId(json, workflowId)))
            throw new NotFoundAppException("AI workflow was not found for this claim.");
    }

    private static bool ContainsWorkflowId(string? json, Guid workflowId)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("remoteAgentRunId", out var value)
                && value.ValueKind == JsonValueKind.String
                && Guid.TryParse(value.GetString(), out var remoteId) && remoteId == workflowId;
        }
        catch (JsonException) { return false; }
    }

    private async Task SynchronizeRunAsync(Guid claimId, Guid workflowId, string status, CancellationToken cancellationToken)
    {
        var run = await _db.AgentRuns.Include(item => item.Steps).FirstOrDefaultAsync(item => item.ClaimId == claimId && item.Objective == "Coordinator claim verification workflow", cancellationToken);
        if (run is null || !ContainsWorkflowId(run.FinalOutcomeJson, workflowId)) return;
        run.Status = status switch
        {
            "waiting_for_approval" => AgentRunStatus.PausedForApproval,
            "completed" => AgentRunStatus.Completed,
            "rejected" or "failed" => AgentRunStatus.Failed,
            _ => AgentRunStatus.Running,
        };
        if (run.Status is AgentRunStatus.Completed or AgentRunStatus.Failed) run.CompletedAt = DateTime.UtcNow;
        var waitingStep = run.Steps.SingleOrDefault(step => step.StepOrder == 2);
        if (waitingStep is not null && status is "completed" or "rejected")
        {
            waitingStep.Status = status == "completed" ? AgentStepStatus.Completed : AgentStepStatus.Failed;
            waitingStep.ErrorMessage = status == "rejected" ? "Human approval rejected." : null;
            waitingStep.CompletedAt = DateTime.UtcNow;
            run.Steps.Add(new AgentStep
            {
                AgentName = AgentName.PlannerAgent,
                StepOrder = 3,
                Task = status == "completed" ? "Coordinator resumed and completed" : "Human approval rejected",
                Status = status == "completed" ? AgentStepStatus.Completed : AgentStepStatus.Failed,
                ErrorMessage = status == "rejected" ? "Human approval rejected." : null,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
            });
        }
        await _db.SaveChangesAsync(cancellationToken);
    }
}
