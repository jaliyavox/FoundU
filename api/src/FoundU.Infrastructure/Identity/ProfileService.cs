using FoundU.Application.Abstractions;
using FoundU.Application.Auth.Dtos;
using FoundU.Application.Common.Exceptions;
using FoundU.Domain.Entities;
using FoundU.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Infrastructure.Identity;

/// <summary>
/// Your own account: name, email, student number and password. Role is not here - nobody
/// promotes themselves - and neither is suspension, which only an admin can lift.
/// </summary>
public class ProfileService : IProfileService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly FoundUDbContext _db;
    private readonly IAuthService _auth;

    public ProfileService(UserManager<AppUser> userManager, FoundUDbContext db, IAuthService auth)
    {
        _userManager = userManager;
        _db = db;
        _auth = auth;
    }

    public async Task<ProfileDto> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(userId);
        return ToDto(user, await _userManager.HasPasswordAsync(user));
    }

    public async Task<ProfileDto> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(userId);
        var hasPassword = await _userManager.HasPasswordAsync(user);
        var newEmail = request.Email.Trim();
        var emailChanged = !string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase);

        if (emailChanged)
        {
            // The address is the sign-in name and the route any reset would take, so moving it
            // needs the password, not just an open tab. An account that has never had one
            // (Google sign-up) has no password to ask for.
            if (hasPassword)
            {
                if (string.IsNullOrEmpty(request.CurrentPassword))
                    throw new ValidationAppException(new Dictionary<string, string[]>
                    {
                        ["CurrentPassword"] = ["Enter your current password to change your email address."],
                    });

                if (!await _userManager.CheckPasswordAsync(user, request.CurrentPassword))
                    throw new ValidationAppException(new Dictionary<string, string[]>
                    {
                        ["CurrentPassword"] = ["That password is not right."],
                    });
            }

            var taken = await _db.Users.AnyAsync(u => u.Id != userId && u.NormalizedEmail == newEmail.ToUpperInvariant(), cancellationToken);
            if (taken)
                throw new ConflictAppException("An account with this email already exists.");

            user.Email = newEmail;
            user.UserName = newEmail; // UserName == Email by convention - see AppUser.cs
            user.NormalizedEmail = _userManager.NormalizeEmail(newEmail);
            user.NormalizedUserName = _userManager.NormalizeName(newEmail);
        }

        user.FullName = request.FullName.Trim();
        user.StudentNumber = string.IsNullOrWhiteSpace(request.StudentNumber) ? null : request.StudentNumber.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded) throw Failed(result);

        return ToDto(user, hasPassword);
    }

    public async Task<AuthResponse> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(userId);
        var hasPassword = await _userManager.HasPasswordAsync(user);

        IdentityResult result;
        if (hasPassword)
        {
            if (string.IsNullOrEmpty(request.CurrentPassword))
                throw new ValidationAppException(new Dictionary<string, string[]>
                {
                    ["CurrentPassword"] = ["Enter your current password."],
                });

            result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        }
        else
        {
            // First password on an account created through Google.
            result = await _userManager.AddPasswordAsync(user, request.NewPassword);
        }

        if (!result.Succeeded) throw Failed(result);

        user.UpdatedAt = DateTime.UtcNow;

        // Every other session goes. Changing a password is how someone takes an account back,
        // and it is worth nothing if a stolen refresh token still works afterwards.
        var active = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in active)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedByIp = ipAddress;
            token.ReasonRevoked = "Password changed";
        }
        await _db.SaveChangesAsync(cancellationToken);

        // ...and this device gets a fresh pair, so the person who just changed it stays in.
        return await _auth.IssueTokensForAsync(user, ipAddress);
    }

    private async Task<AppUser> FindAsync(Guid userId)
        => await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundAppException("Account not found.");

    private static ValidationAppException Failed(IdentityResult result)
        => new(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));

    private static ProfileDto ToDto(AppUser user, bool hasPassword) => new(
        user.Id,
        user.FullName,
        user.Email!,
        user.Role.ToString(),
        user.StudentNumber,
        hasPassword,
        user.GoogleSubjectId is not null,
        user.CreatedAt);
}
