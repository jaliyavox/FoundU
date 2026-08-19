using System.Text;
using FluentValidation;
using FoundU.Application.Abstractions;
using FoundU.Application.Auth;
using FoundU.Application.Auth.Validators;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Administration;
using FoundU.Infrastructure.Identity;
using FoundU.Infrastructure.Persistence;
using FoundU.Infrastructure.Storage;
using FoundU.Infrastructure.Reporting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace FoundU.Infrastructure;

/// <summary>
/// Single place FoundU.Api calls to wire up the database, ASP.NET Core Identity, JWT bearer
/// authentication, the three role policies, and application services. Keeps Program.cs short.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddFoundUInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<FoundUDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("FoundUDatabase")));

        services.AddIdentityCore<AppUser>(options =>
            {
                // Password policy - mirrored client-side in RegisterRequestValidator so the
                // client gets a fast, clear 400 instead of waiting for Identity's own error.
                options.Password.RequiredLength = 8;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireDigit = true;
                options.Password.RequireNonAlphanumeric = false;

                options.User.RequireUniqueEmail = true;

                // Brute-force protection: 5 bad passwords locks the account for 15 minutes.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<FoundUDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .Validate(settings => IsUsableSigningKey(settings.SigningKey),
                "Jwt:SigningKey must be a non-placeholder secret of at least 32 UTF-8 bytes. Configure it with User Secrets or the Jwt__SigningKey environment variable.")
            .ValidateOnStart();

        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException("Missing 'Jwt' configuration section.");

        if (!IsUsableSigningKey(jwtSettings.SigningKey))
            throw new InvalidOperationException(
                "Jwt:SigningKey must be a non-placeholder secret of at least 32 UTF-8 bytes. Configure it with User Secrets or the Jwt__SigningKey environment variable.");

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorizationBuilder()
            // Student is deliberately exclusive: "acts as a student on their own reports" is a
            // different capability from administering the system, so Admin is NOT included here.
            .AddPolicy(PolicyNames.Student, p => p.RequireRole(nameof(UserRole.Student)))
            // Staff means "staff-level access", which an Admin also has - otherwise an Admin
            // could not work the lost-and-found desk they administer.
            .AddPolicy(PolicyNames.Staff, p => p.RequireRole(nameof(UserRole.Staff), nameof(UserRole.Admin)))
            .AddPolicy(PolicyNames.Admin, p => p.RequireRole(nameof(UserRole.Admin)));

        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IReferenceDataService, ReferenceDataService>();
        services.AddScoped<IFoundReportService, FoundReportService>();
        services.AddScoped<ILostReportService, LostReportService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddSingleton<IPhotoStorage, LocalPhotoStorage>();

        services.AddValidatorsFromAssembly(typeof(RegisterRequestValidator).Assembly);

        return services;
    }

    private static bool IsUsableSigningKey(string? signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
            return false;

        return !signingKey.Contains("replace", StringComparison.OrdinalIgnoreCase)
            && !signingKey.Contains("change-me", StringComparison.OrdinalIgnoreCase)
            && !signingKey.Contains("your_", StringComparison.OrdinalIgnoreCase)
            && !signingKey.Contains("placeholder", StringComparison.OrdinalIgnoreCase);
    }
}
