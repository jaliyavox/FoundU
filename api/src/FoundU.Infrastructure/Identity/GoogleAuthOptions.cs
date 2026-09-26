namespace FoundU.Infrastructure.Identity;

public sealed class GoogleAuthOptions
{
    public const string SectionName = "Google";

    /// <summary>
    /// The OAuth client id from Google Cloud Console. Empty means Google sign-in is switched
    /// off: the endpoint says so plainly and the clients hide the button.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Where Google publishes the keys its ID tokens are signed with.</summary>
    public string JwksUri { get; set; } = "https://www.googleapis.com/oauth2/v3/certs";

    /// <summary>How long a fetched key set is reused before it is fetched again.</summary>
    public int KeyCacheMinutes { get; set; } = 60;
}
