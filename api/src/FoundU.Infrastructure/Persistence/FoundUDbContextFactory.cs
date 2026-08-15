using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FoundU.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef migrations add` / `dotnet ef database update` run directly against
/// FoundU.Infrastructure without needing to start the full FoundU.Api host.
/// Reads appsettings, user secrets, and environment variables at design time.
/// </summary>
public class FoundUDbContextFactory : IDesignTimeDbContextFactory<FoundUDbContext>
{
    public FoundUDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("src/FoundU.Api/appsettings.json", optional: true)
            .AddJsonFile("src/FoundU.Api/appsettings.Development.json", optional: true)
            .AddUserSecrets(
                "4a4f4bb2-c349-4b30-9db6-af3af8a4ea16")
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            config.GetConnectionString("FoundUDatabase")
            ?? Environment.GetEnvironmentVariable("FOUNDU_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "Connection string 'FoundUDatabase' was not configured.");

        var optionsBuilder =
            new DbContextOptionsBuilder<FoundUDbContext>();

        optionsBuilder.UseNpgsql(connectionString);

        return new FoundUDbContext(optionsBuilder.Options);
    }
}