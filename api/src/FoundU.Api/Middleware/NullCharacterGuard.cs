using System.Text.Json;
using System.Text.Json.Serialization;

namespace FoundU.Api.Middleware;

/// <summary>
/// PostgreSQL cannot store a NUL (U+0000) in text, so a search or a field containing one used to
/// reach the database and come back as a 500 (found by the OWASP ZAP API scan, Assignment 2).
/// It is never meaningful input, so it is refused at the edge as a 400 instead.
/// </summary>
public static class NullCharacterGuard
{
    public const string Message = "Text cannot contain a null character.";

    /// <summary>Refuses a query string or path that carries an encoded NUL.</summary>
    public static async Task RejectInQueryOrPath(HttpContext context, Func<Task> next)
    {
        var query = context.Request.QueryString.Value ?? string.Empty;
        var path = context.Request.Path.Value ?? string.Empty;
        if (query.Contains("%00", StringComparison.Ordinal) || query.Contains('\0') || path.Contains('\0'))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { title = "Bad request", status = 400, detail = Message });
            return;
        }
        await next();
    }
}

/// <summary>Every JSON string the API reads: one with a NUL in it fails model binding (400).</summary>
public sealed class NoNullCharacterStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (value is not null && value.Contains('\0'))
            throw new JsonException(NullCharacterGuard.Message);
        return value;
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
