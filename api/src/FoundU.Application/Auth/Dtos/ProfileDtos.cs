namespace FoundU.Application.Auth.Dtos;

/// <summary>
/// Editing your own account. Changing the email address asks for the current password: the
/// address is how you sign in and how a reset would reach you, so a borrowed, unlocked
/// session must not be enough to move it.
/// </summary>
public record UpdateProfileRequest(
    string FullName,
    string Email,
    string? StudentNumber,
    string? CurrentPassword);

/// <summary>
/// <c>CurrentPassword</c> is null only for an account that has never had one - someone who
/// signed up with Google and is setting their first password.
/// </summary>
public record ChangePasswordRequest(string? CurrentPassword, string NewPassword);

/// <summary>What the profile screen shows, read from the database rather than the token claims.</summary>
public record ProfileDto(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    string? StudentNumber,
    bool HasPassword,
    bool IsGoogleLinked,
    DateTime CreatedAt,
    /// <summary>The address has been proved by a link sent to it. Unconfirmed accounts get a reminder.</summary>
    bool EmailConfirmed = false);

/// <summary>The ID token Google's button hands the client. Never a Google access token.</summary>
public record GoogleSignInRequest(string IdToken);

/// <summary>What a verified Google ID token tells us. Nothing here is trusted until verified.</summary>
public record GoogleIdentity(string Subject, string Email, bool EmailVerified, string? Name);
