using FoundU.Application.Common;
using Xunit;

namespace FoundU.Tests;

public class AppInfoTests
{
    [Fact]
    public void Describe_ReturnsProjectName()
    {
        Assert.Equal("FoundU API", AppInfo.Describe());
    }
}

public sealed class ConnectionStringTests
{
    [Fact]
    public void ARenderStyleUrlBecomesAnNpgsqlConnectionString()
    {
        var value = FoundU.Infrastructure.Persistence.ConnectionStrings.Normalize(
            "postgres://foundu_user:p%40ss%3Aword@dpg-abc123-a.oregon-postgres.render.com/foundu_db");

        var parsed = new Npgsql.NpgsqlConnectionStringBuilder(value);
        Assert.Equal("dpg-abc123-a.oregon-postgres.render.com", parsed.Host);
        Assert.Equal(5432, parsed.Port);
        Assert.Equal("foundu_db", parsed.Database);
        Assert.Equal("foundu_user", parsed.Username);
        Assert.Equal("p@ss:word", parsed.Password);
    }

    [Fact]
    public void AKeyValueStringIsLeftAlone()
    {
        const string local = "Host=localhost;Port=5434;Database=foundu;Username=foundu;Password=foundu";
        Assert.Equal(local, FoundU.Infrastructure.Persistence.ConnectionStrings.Normalize(local));
    }
}

public sealed class NeonConnectionStringTests
{
    [Fact]
    public void ANeonUrlKeepsItsTlsRequirement()
    {
        // The shape Neon's dashboard hands out.
        var value = FoundU.Infrastructure.Persistence.ConnectionStrings.Normalize(
            "postgresql://neondb_owner:npg_AbC123@ep-cool-sun-a1b2c3d4.ap-southeast-1.aws.neon.tech/neondb?sslmode=require&channel_binding=require");

        var parsed = new Npgsql.NpgsqlConnectionStringBuilder(value);
        Assert.Equal("ep-cool-sun-a1b2c3d4.ap-southeast-1.aws.neon.tech", parsed.Host);
        Assert.Equal("neondb", parsed.Database);
        Assert.Equal("neondb_owner", parsed.Username);
        Assert.Equal("npg_AbC123", parsed.Password);
        Assert.Equal(Npgsql.SslMode.Require, parsed.SslMode);
    }
}
