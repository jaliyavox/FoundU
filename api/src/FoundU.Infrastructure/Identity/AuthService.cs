using FoundU.Application.Abstractions;
using FoundU.Application.Auth.Dtos;
using FoundU.Application.Common.Exceptions;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FoundU.Infrastructure.Identity;

public class AuthService : IAuthService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly FoundUDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        FoundUDbContext db,
        ITokenService tokenService,
        IOptions<JwtSettings> jwtSettings)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _tokenService = tokenService;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ipAddress)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null)
        {
            throw new ConflictAppException("An account with this email already exists.");
        }

        // Self-registration is Students only - see RegisterRequest.cs. Staff/Admin accounts are
        // created through a separate Admin-only management endpoint, not this one.
        var user = new AppUser
        {
            UserName = request.Email, // UserName == Email by convention (AppUser.cs)
            Email = request.Email,
            FullName = request.FullName,
            StudentNumber = request.StudentNumber,
            Role = UserRole.Student
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.ToDictionary(
                e => e.Code,
                e => new[] { e.Description });
            throw new ValidationAppException(errors);
        }

        return await IssueTokensAsync(user, ipAddress);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            throw new UnauthorizedAppException();
        }

        if (user.IsSuspended)
        {
            throw new ForbiddenAppException("This account has been suspended. Contact an administrator.");
        }

        // lockoutOnFailure: true - after IdentityOptions.Lockout.MaxFailedAccessAttempts
        // consecutive bad passwords, the account is locked out for a cooldown window,
        // protecting against brute-force guessing.
        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            throw new UnauthorizedAppException();
        }

        return await IssueTokensAsync(user, ipAddress);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, string? ipAddress)
    {
        var tokenHash = _tokenService.HashToken(refreshToken);

        var existing = await _db.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (existing is null)
        {
            throw new UnauthorizedAppException("Invalid refresh token.");
        }

        if (existing.IsRevoked)
        {
            // Reuse of an already-rotated token is a strong signal the token was stolen -
            // revoke every active refresh token this user has as a precaution.
            var activeTokens = await _db.RefreshTokens
                .Where(t => t.UserId == existing.UserId && t.RevokedAt == null)
                .ToListAsync();

            foreach (var t in activeTokens)
            {
                t.RevokedAt = DateTime.UtcNow;
                t.RevokedByIp = ipAddress;
                t.ReasonRevoked = "Possible token reuse detected";
            }

            await _db.SaveChangesAsync();
            throw new UnauthorizedAppException("This refresh token has already been used. All sessions have been revoked - please log in again.");
        }

        if (existing.IsExpired)
        {
            throw new UnauthorizedAppException("Refresh token has expired.");
        }

        if (existing.User.IsSuspended)
        {
            throw new ForbiddenAppException("This account has been suspended. Contact an administrator.");
        }

        var newRawToken = _tokenService.GenerateRefreshTokenValue();
        var newTokenHash = _tokenService.HashToken(newRawToken);
        var refreshExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenDays);

        existing.RevokedAt = DateTime.UtcNow;
        existing.RevokedByIp = ipAddress;
        existing.ReplacedByTokenHash = newTokenHash;
        existing.ReasonRevoked = "Rotated on refresh";

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = existing.UserId,
            TokenHash = newTokenHash,
            ExpiresAt = refreshExpiresAt,
            CreatedByIp = ipAddress
        });

        await _db.SaveChangesAsync();

        var accessToken = _tokenService.GenerateAccessToken(existing.User);

        return new AuthResponse(
            accessToken.Value,
            accessToken.ExpiresAtUtc,
            newRawToken,
            refreshExpiresAt,
            ToUserDto(existing.User));
    }

    public async Task RevokeAsync(string refreshToken, string? ipAddress)
    {
        var tokenHash = _tokenService.HashToken(refreshToken);

        var existing = await _db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash);
        if (existing is null || existing.IsRevoked)
        {
            return; // idempotent - logging out an already-revoked/unknown token is not an error
        }

        existing.RevokedAt = DateTime.UtcNow;
        existing.RevokedByIp = ipAddress;
        existing.ReasonRevoked = "Logged out";

        await _db.SaveChangesAsync();
    }

    private async Task<AuthResponse> IssueTokensAsync(AppUser user, string? ipAddress)
    {
        var accessToken = _tokenService.GenerateAccessToken(user);
        var rawRefreshToken = _tokenService.GenerateRefreshTokenValue();
        var refreshExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenDays);

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _tokenService.HashToken(rawRefreshToken),
            ExpiresAt = refreshExpiresAt,
            CreatedByIp = ipAddress
        });

        await _db.SaveChangesAsync();

        return new AuthResponse(
            accessToken.Value,
            accessToken.ExpiresAtUtc,
            rawRefreshToken,
            refreshExpiresAt,
            ToUserDto(user));
    }

    private static UserDto ToUserDto(AppUser user) => new(
        user.Id,
        user.FullName,
        user.Email ?? string.Empty,
        user.Role.ToString(),
        user.StudentNumber,
        user.IsSuspended);
}
