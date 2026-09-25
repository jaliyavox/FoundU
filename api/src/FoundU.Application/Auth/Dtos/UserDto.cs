namespace FoundU.Application.Auth.Dtos;

/// <summary>Safe-to-expose user projection. Never includes PasswordHash, SecurityStamp, or any Identity internals.</summary>
public record UserDto(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    string? StudentNumber,
    bool IsSuspended);
