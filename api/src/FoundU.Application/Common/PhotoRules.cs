namespace FoundU.Application.Common;

/// <summary>
/// Upload limits, shared by the validator and the storage layer. The client enforces these
/// too for a fast error, but the client's copy is advisory - anyone can POST straight to the
/// endpoint, so these are the ones that actually count.
/// </summary>
public static class PhotoRules
{
    public const int MaxPhotosPerReport = 2;
    public const long MaxBytes = 5 * 1024 * 1024; // 5 MB
    public const string MaxSizeLabel = "5 MB";

    public static readonly string[] AllowedContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
    ];

    /// <summary>
    /// Checks the file's leading bytes rather than trusting its extension or the declared
    /// content type - both are attacker-controlled. Returns the extension to save it under.
    /// </summary>
    public static string? ResolveExtension(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return ".jpg";

        if (header.Length >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return ".png";

        // RIFF....WEBP
        if (header.Length >= 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            return ".webp";

        return null;
    }
}
