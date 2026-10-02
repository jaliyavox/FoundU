using FoundU.Application.Email;
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
    private readonly IGoogleTokenVerifier _google;

    private readonly IAccountEmailService _emails;

    public AuthService(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        FoundUDbContext db,
        ITokenService tokenService,
        IOptions<JwtSettings> jwtSettings,
        IGoogleTokenVerifier google,
        IAccountEmailService emails)
    {
        _emails = emails;
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _tokenService = tokenService;
        _jwtSettings = jwtSettings.Value;
        _google = google;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ipAddress)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null)
        {
            throw new ConflictAppException("An account with this email already exists.");
        }

        var studentNumber = string.IsNullOrWhiteSpace(request.StudentNumber) ? null : request.StudentNumber.Trim();
        if (studentNumber is not null && await _userManager.Users.AnyAsync(u => u.StudentNumber == studentNumber))
        {
            throw new ConflictAppException("An account with this student number already exists.");
        }

        // Self-registration is Students only - see RegisterRequest.cs. Staff accounts are not
        // created through the API; see docs/testing for how the demo seed provisions them.
        var user = new AppUser
        {
            UserName = request.Email, // UserName == Email by convention (AppUser.cs)
            Email = request.Email,
            FullName = request.FullName,
            StudentNumber = studentNumber,
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

        // Signed in straight away; the address is proved later from the email. A failed send
        // must not fail the sign-up - the account page offers to send it again.
        try
        {
            await _emails.SendConfirmationAsync(user.Id);
        }
        catch (AppException)
        {
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

        // lockoutOnFailure: true - after IdentityOptions.Lockout.MaxFailedAccessAttempts
        // consecutive bad passwords, the account is locked out for a cooldown window,
        // protecting against brute-force guessing.
        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            throw new UnauthorizedAppException();
        }

        // Checked after the password, so an email alone does not reveal that an account is suspended.
        if (user.IsSuspended)
        {
            throw new ForbiddenAppException("This account has been suspended. Contact an administrator.");
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
            // A token that was revoked deliberately - logged out, or a password change that
            // ended the other sessions - is simply dead. Only a *rotated* token coming back
            // is evidence of theft, and ReplacedByTokenHash is what tells the two apart.
            // Without that distinction, one stale client retrying after a password change
            // would revoke the session the person had just re-established.
            if (existing.ReplacedByTokenHash is null)
                throw new UnauthorizedAppException("Invalid refresh token.");

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

    public async Task<AuthResponse> GoogleSignInAsync(GoogleSignInRequest request, string? ipAddress)
    {
        if (!_google.IsConfigured)
            throw new ConflictAppException("Google sign-in is not set up on this server.");

        var identity = await _google.VerifyAsync(request.IdToken)
            ?? throw new UnauthorizedAppException("That Google sign-in could not be verified.");

        // Matched on the subject id first: it is stable, while an email address can be
        // reassigned to somebody else inside an organisation.
        var user = await _db.Users.FirstOrDefaultAsync(u => u.GoogleSubjectId == identity.Subject);

        if (user is null)
        {
            var normalised = _userManager.NormalizeEmail(identity.Email);
            var byEmail = await _db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalised);

            if (byEmail is not null)
            {
                // Linking an existing account by email is only safe when Google says it has
                // confirmed the address - otherwise anyone who can make a Google account with
                // someone else's address could walk into their FoundU account.
                if (!identity.EmailVerified)
                    throw new UnauthorizedAppException("Google has not verified that email address.");

                byEmail.GoogleSubjectId = identity.Subject;
                byEmail.UpdatedAt = DateTime.UtcNow;
                user = byEmail;
            }
            else
            {
                if (!identity.EmailVerified)
                    throw new UnauthorizedAppException("Google has not verified that email address.");

                // Self-service accounts are Students, exactly as with the registration form.
                user = new AppUser
                {
                    UserName = identity.Email,
                    Email = identity.Email,
                    EmailConfirmed = true,
                    FullName = string.IsNullOrWhiteSpace(identity.Name) ? identity.Email.Split('@')[0] : identity.Name,
                    Role = UserRole.Student,
                    GoogleSubjectId = identity.Subject,
                };

                var created = await _userManager.CreateAsync(user);
                if (!created.Succeeded)
                    throw new ValidationAppException(created.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
            }
        }

        if (user.IsSuspended)
            throw new ForbiddenAppException("This account has been suspended. Contact an administrator.");

        return await IssueTokensAsync(user, ipAddress);
    }

    public Task<AuthResponse> IssueTokensForAsync(AppUser user, string? ipAddress) => IssueTokensAsync(user, ipAddress);

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
