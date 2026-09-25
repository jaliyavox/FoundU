using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FoundU.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef migrations add` / `dotnet ef database update` run directly against
/// FoundU.Infrastructure without needing to start the full FoundU.Api host.
/// Reads the connection string from FoundU.Api/appsettings.json (or env var) at design time only.
/// </summary>
public class FoundUDbContextFactory : IDesignTimeDbContextFactory<FoundUDbContext>
{
    public FoundUDbContext CreateDbContext(string[] args)
    {
        // `dotnet ef` runs from the build output folder, not the project folder, so a relative
        // "../FoundU.Api" path silently misses every file and leaves the config empty.
        var apiDirectory = ResolveApiDirectory();

        var config = new ConfigurationBuilder()
            .SetBasePath(apiDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Deliberately no hardcoded fallback: a wrong-but-plausible default connects to whatever
        // is on localhost:5432 and fails later with a confusing error, instead of here.
        var connectionString = config.GetConnectionString("FoundUDatabase")
            ?? Environment.GetEnvironmentVariable("FOUNDU_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                $"No connection string found. Looked for ConnectionStrings:FoundUDatabase in " +
                $"{apiDirectory}/appsettings.Development.json and the FOUNDU_CONNECTION_STRING " +
                "environment variable.");

        var optionsBuilder = new DbContextOptionsBuilder<FoundUDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new FoundUDbContext(optionsBuilder.Options);
    }

    /// <summary>
    /// Walks up from the assembly's output folder (bin/Debug/net8.0) until it finds the sibling
    /// FoundU.Api project directory, so design-time config resolution does not depend on the
    /// working directory the EF tooling happens to use.
    /// </summary>
    private static string ResolveApiDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "FoundU.Api");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
