using Microsoft.AspNetCore.HttpOverrides;
using FoundU.Infrastructure.Persistence;
using FoundU.Api.Filters;
using FoundU.Api.Middleware;
using FoundU.Domain.Entities;
using FoundU.Infrastructure;
using FoundU.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;
using Serilog;

// Bootstrap logger - active before the full DI container exists, so startup failures are logged too.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

builder.Services.AddControllers(options =>
    {
        // Runs FluentValidation against every request DTO before the action executes -
        // see /docs/api/conventions.md "Validation".
        options.Filters.Add<ValidationFilter>();
    });

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "FoundU API", Version = "v1" });

        var bearerScheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the access token only - Swagger adds the 'Bearer ' prefix automatically.",
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        };

        options.AddSecurityDefinition("Bearer", bearerScheme);
        options.AddSecurityRequirement(new OpenApiSecurityRequirement { { bearerScheme, Array.Empty<string>() } });
    });

builder.Services.AddFoundUInfrastructure(builder.Configuration);

    // .NET 8 IExceptionHandler pipeline - GlobalExceptionHandler turns every exception into the
    // standard ProblemDetails envelope. See /docs/api/conventions.md "Error envelope".
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? new[]
        {
            "http://localhost:5173",
            "http://localhost:3000",
            "http://localhost:2106",
            "http://localhost:11836",
            "http://127.0.0.1:11836"
        };

builder.Services.AddCors(options =>
    {
        options.AddPolicy("ReactDev", policy =>
        {
            policy.SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrWhiteSpace(origin))
                    return false;

                if (allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
                    return true;

                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    return false;

                return uri.Host is "localhost" or "127.0.0.1" or "::1";
            });

            policy.AllowAnyHeader();
            policy.AllowAnyMethod();
            policy.AllowCredentials();
        });
    });

var app = builder.Build();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

// Behind a hosting proxy (Render) the request arrives as plain HTTP from the proxy. Trust its
// forwarded headers so the API sees the caller's scheme and address - otherwise HTTPS
// redirection loops and every audit row records the proxy's IP. The proxy's address is not
// fixed, so no network list is pinned; nothing here is exposed except through it.
var forwarded = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
forwarded.KnownNetworks.Clear();
forwarded.KnownProxies.Clear();
app.UseForwardedHeaders(forwarded);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Migrations and starter data (categories, places, the admin) on every start: the seeder is
// idempotent. Deployed, it insists on a configured admin password rather than the dev one.
// Database:MigrateOnStartup=false skips it, for a host that migrates some other way.
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    await DevelopmentDataSeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(),
        scope.ServiceProvider.GetRequiredService<FoundUDbContext>(),
        scope.ServiceProvider.GetRequiredService<IConfiguration>(),
        allowFallbackPassword: app.Environment.IsDevelopment());
}

// Uploaded photos are served from wwwroot. The feed is public, so these are too.
//
// The directory is created here rather than relied upon: if wwwroot does not exist when the
// host starts, WebRootPath is null and UseStaticFiles() silently serves nothing - uploads
// save fine and then 404. An explicit provider makes the root unambiguous either way.
var webRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(webRoot);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(webRoot),
});

app.UseHttpsRedirection();
app.UseCors("ReactDev");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposed so WebApplicationFactory-based integration tests can reference the entry point.
}
catch (HostAbortedException)
{
    throw;
}
catch (Exception ex)
{
    Log.Fatal(ex, "FoundU API terminated unexpectedly during startup");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
