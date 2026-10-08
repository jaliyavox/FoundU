using FoundU.Application.Abstractions;
using FoundU.Domain.Entities;
using FoundU.Infrastructure.Persistence;

namespace FoundU.Infrastructure.Storage;

/// <summary>
/// Keeps uploaded photos in the database and serves them from <c>/api/photos/{id}</c>.
///
/// The photo is added to the caller's unit of work, not saved on its own: the report's photo
/// row and the photo itself are committed by the same SaveChanges, so neither can exist
/// without the other. The id in the URL is a random GUID, never reused, so it is safe to
/// cache for good.
/// </summary>
public class DatabasePhotoStorage(FoundUDbContext db) : IPhotoStorage
{
    public const string RoutePrefix = "/api/photos/";

    public async Task<string> SaveAsync(PhotoUpload upload, string folder, CancellationToken cancellationToken = default)
    {
        // The caller has already sniffed the bytes and passes the extension as the file name.
        var extension = Path.GetExtension(upload.FileName).ToLowerInvariant();
        var contentType = extension switch
        {
            ".jpg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => throw new InvalidOperationException("Only JPEG, PNG and WebP photos are stored."),
        };

        using var buffer = new MemoryStream();
        await upload.Content.CopyToAsync(buffer, cancellationToken);

        var photo = new StoredPhoto { ContentType = contentType, Data = buffer.ToArray() };
        db.StoredPhotos.Add(photo);

        // The extension is for people and browsers; the id alone finds the photo.
        return $"{RoutePrefix}{photo.Id:N}{extension}";
    }

    public Task DeleteAsync(string url, CancellationToken cancellationToken = default)
    {
        if (TryReadId(url, out var id))
        {
            // Removed with the caller's SaveChanges, like a save.
            db.StoredPhotos.Remove(new StoredPhoto { Id = id });
        }
        return Task.CompletedTask;
    }

    /// <summary>"/api/photos/3f2a....jpg" or just the id -> the photo's id.</summary>
    public static bool TryReadId(string urlOrName, out Guid id)
    {
        var name = urlOrName.StartsWith(RoutePrefix, StringComparison.OrdinalIgnoreCase)
            ? urlOrName[RoutePrefix.Length..]
            : urlOrName;
        var dot = name.IndexOf('.');
        return Guid.TryParse(dot < 0 ? name : name[..dot], out id);
    }
}
