using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoundU.Tests;

/// <summary>Explicit opt-in, migration-backed PostgreSQL test database guard.</summary>
internal static class PostgresTestDatabase
{
    private const string VariableName = "TEST_DATABASE_URL";

    public static string? SkipReason
    {
        get
        {
            var connectionString = Environment.GetEnvironmentVariable(VariableName);
            if (string.IsNullOrWhiteSpace(connectionString))
                return $"Set {VariableName} to run PostgreSQL integration tests.";
            try
            {
                var builder = new NpgsqlConnectionStringBuilder(connectionString!);
                return builder.Database?.Contains("test", StringComparison.OrdinalIgnoreCase) == true
                    ? null
                    : $"{VariableName} must name a clearly test-only database.";
            }
            catch (ArgumentException)
            {
                return $"{VariableName} is not a valid PostgreSQL connection string.";
            }
        }
    }

    public static FoundUDbContext CreateContext()
    {
        if (SkipReason is { } reason) throw new InvalidOperationException(reason);
        return new FoundUDbContext(new DbContextOptionsBuilder<FoundUDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable(VariableName))
            .Options);
    }

    public static async Task MigrateAsync(FoundUDbContext db) => await db.Database.MigrateAsync();
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute() => Skip = PostgresTestDatabase.SkipReason;
}
