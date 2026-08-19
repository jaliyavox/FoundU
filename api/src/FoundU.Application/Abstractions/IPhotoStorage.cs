namespace FoundU.Application.Abstractions;

/// <summary>A file handed to the API, described without depending on ASP.NET's IFormFile.</summary>
public record PhotoUpload(string FileName, string ContentType, long Length, Stream Content);

/// <summary>
/// Where uploaded photos live. Local disk today; swapping in object storage later means one
/// new implementation and no change to the services that call this.
/// </summary>
public interface IPhotoStorage
{
    /// <summary>Saves the file and returns the URL it will be served from.</summary>
    Task<string> SaveAsync(PhotoUpload upload, string folder, CancellationToken cancellationToken = default);

    Task DeleteAsync(string url, CancellationToken cancellationToken = default);
}
