using System.Text;
using FluentValidation;
using FoundU.Application.Abstractions;
using FoundU.Application.Auth;
using FoundU.Application.Auth.Validators;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Identity;
using FoundU.Infrastructure.Persistence;
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
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<FoundUDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException("Missing 'Jwt' configuration section.");

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
            .AddPolicy(PolicyNames.Student, p => p.RequireRole(nameof(UserRole.Student)))
            .AddPolicy(PolicyNames.Staff, p => p.RequireRole(nameof(UserRole.Staff)))
            .AddPolicy(PolicyNames.Admin, p => p.RequireRole(nameof(UserRole.Admin)));

        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddValidatorsFromAssembly(typeof(RegisterRequestValidator).Assembly);

        return services;
    }
}
