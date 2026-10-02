using FoundU.Application.Auth.Dtos;

namespace FoundU.Application.Abstractions;

/// <summary>
/// Verifies a Google ID token's signature, issuer, audience and expiry against Google's
/// published keys. Returns null for anything that does not check out - the caller must treat
/// null as "not signed in", never as "probably fine".
/// </summary>
public interface IGoogleTokenVerifier
{
    Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken cancellationToken = default);

    /// <summary>False when no client id is configured, so the endpoint can say so plainly.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// The OAuth client id, or null when switched off. Public by design - it is in every
    /// Google sign-in page - so the web and phone clients read it from here instead of each
    /// carrying their own copy that can drift from the one tokens are checked against.
    /// </summary>
    string? ClientId { get; }
}
