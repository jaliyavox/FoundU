using Npgsql;

namespace FoundU.Infrastructure.Persistence;

/// <summary>
/// Hosts such as Render hand out the database as a URL - postgres://user:pass@host:5432/db.
/// Npgsql reads key=value pairs, so a URL is translated; anything else is passed through.
/// </summary>
public static class ConnectionStrings
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var trimmed = value.Trim();
        if (!trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        var uri = new Uri(trimmed);
        var credentials = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : null,
            // A host's internal network may not offer TLS; its external address will. Prefer
            // covers both without a per-environment setting.
            SslMode = SslMode.Prefer,
        };

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase) && parts.Length == 2
                && Enum.TryParse<SslMode>(parts[1], ignoreCase: true, out var mode))
                builder.SslMode = mode;
        }

        return builder.ConnectionString;
    }
}
