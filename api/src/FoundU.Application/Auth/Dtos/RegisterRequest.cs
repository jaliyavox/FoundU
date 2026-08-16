namespace FoundU.Application.Auth.Dtos;

/// <summary>
/// Self-registration is for Students only (see /docs/api/conventions.md "Auth flow" -
/// Staff/Admin accounts are created by an Admin via a separate management endpoint, not this one).
/// </summary>
public record RegisterRequest(
    string FullName,
    string Email,
    string Password,
    string? StudentNumber);
