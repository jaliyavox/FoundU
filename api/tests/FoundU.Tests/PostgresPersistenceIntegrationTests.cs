using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

namespace FoundU.Tests;

/// <summary>
/// Group-owned schema checks. Each member's own PostgreSQL tests are in their folder
/// (Member{N}DatabaseTests.cs); the shared seed data is in PostgresTestSupport.
/// </summary>
[Trait("Category", "PostgreSql")]
[Trait("Member", "Group")]
public sealed class PostgresPersistenceIntegrationTests
{
    [PostgresFact]
    public async Task MigrationsApplyAndModelHasNoPendingMigrations()
    {
        await using var db = PostgresTestDatabase.CreateContext();
        await PostgresTestDatabase.MigrateAsync(db);

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.True(await db.Database.CanConnectAsync());
    }

    [PostgresFact]
    public async Task AgentRunJsonbAndAgentStepForeignKeyRoundTrip()
    {
        await using var db = PostgresTestDatabase.CreateContext();
        await PostgresTestDatabase.MigrateAsync(db);
        var run = new AgentRun
        {
            TriggerEntityType = "PostgresIntegration",
            TriggerEntityId = Guid.NewGuid(),
            Objective = "PostgreSQL JSONB audit round-trip",
            PlanJson = "{\"steps\":[\"planning\"]}",
            FinalOutcomeJson = "{\"outcome\":\"safe\",\"retryCount\":2}",
            RetryCount = 2,
            Status = AgentRunStatus.Completed,
            CompletedAt = DateTime.UtcNow,
        };
        db.AgentRuns.Add(run);
        await db.SaveChangesAsync();
        db.AgentSteps.Add(new AgentStep
        {
            AgentRunId = run.Id,
            AgentName = AgentName.PlannerAgent,
            StepOrder = 1,
            Task = "Coordinator planning",
            Status = AgentStepStatus.Completed,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var reloaded = await db.AgentRuns.Include(item => item.Steps).SingleAsync(item => item.Id == run.Id);
        Assert.NotNull(reloaded.PlanJson);
        Assert.NotNull(reloaded.FinalOutcomeJson);
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("{\"steps\":[\"planning\"]}"),
            JsonNode.Parse(reloaded.PlanJson!)));
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("{\"outcome\":\"safe\",\"retryCount\":2}"),
            JsonNode.Parse(reloaded.FinalOutcomeJson!)));
        Assert.Equal(2, reloaded.RetryCount);
        Assert.Single(reloaded.Steps);
    }
}
