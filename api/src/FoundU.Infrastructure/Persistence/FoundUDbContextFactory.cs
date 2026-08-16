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
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("../FoundU.Api/appsettings.json", optional: true)
            .AddJsonFile("../FoundU.Api/appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("FoundUDatabase")
            ?? Environment.GetEnvironmentVariable("FOUNDU_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=foundu;Username=postgres;Password=YOUR_PASSWORD";

        var optionsBuilder = new DbContextOptionsBuilder<FoundUDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new FoundUDbContext(optionsBuilder.Options);
    }
}
