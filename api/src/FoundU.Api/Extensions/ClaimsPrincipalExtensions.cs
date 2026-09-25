using System.Security.Claims;
using FoundU.Domain.Enums;
using FoundU.Application.Common.Exceptions;

namespace FoundU.Api.Extensions;

/// <summary>
/// Reads the caller's identity out of the validated JWT. Controllers must never take a user id
/// from the request body - that would let anyone act as anyone else.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        if (!Guid.TryParse(raw, out var userId))
        {
            // The endpoint requires [Authorize], so a token without a usable subject is malformed.
            throw new UnauthorizedAppException("The access token does not contain a valid user id.");
        }

        return userId;
    }

    /// <summary>
    /// The caller's id, or null when they are not signed in. For [AllowAnonymous] endpoints
    /// that behave the same either way but can say a little more to someone signed in - a
    /// missing or malformed token is not an error there, it is just anonymous.
    /// </summary>
    public static Guid? GetUserIdOrNull(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        return Guid.TryParse(raw, out var userId) ? userId : null;
    }

    public static bool IsStaffOrAdmin(this ClaimsPrincipal principal)
        => principal.IsInRole(nameof(UserRole.Staff)) || principal.IsInRole(nameof(UserRole.Admin));
}
