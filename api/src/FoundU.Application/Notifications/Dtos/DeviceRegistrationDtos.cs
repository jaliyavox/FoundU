namespace FoundU.Application.Notifications.Dtos;

/// <summary>Request payload only; device tokens are never returned from FoundU APIs.</summary>
public record RegisterDeviceRequest(string Token, string Platform);

/// <summary>Request payload only; callers may deactivate only a token they currently own.</summary>
public record UnregisterDeviceRequest(string Token);
