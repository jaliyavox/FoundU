using FoundU.Infrastructure.Persistence;
using FoundU.Api.Filters;
using FoundU.Api.Middleware;
using FoundU.Domain.Entities;
using FoundU.Infrastructure;
using FoundU.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Identity;
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
        ?? new[] { "http://localhost:5173", "http://localhost:3000" };

 builder.Services.AddCors(options =>
    {
        options.AddPolicy("ReactDev", policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
    });

var app = builder.Build();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
        await DevelopmentDataSeeder.SeedAsync(
            scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(),
            scope.ServiceProvider.GetRequiredService<FoundUDbContext>(),
            scope.ServiceProvider.GetRequiredService<IConfiguration>());
    }

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
