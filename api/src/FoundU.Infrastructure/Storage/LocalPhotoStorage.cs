using FoundU.Application.Abstractions;
using Microsoft.Extensions.Hosting;

namespace FoundU.Infrastructure.Storage;

/// <summary>
/// Saves uploads under the API's wwwroot so they can be served as static files.
///
/// Filenames are generated, never taken from the upload: a client-supplied name is the
/// classic path-traversal vector ("../../appsettings.json"), and two people uploading
/// "photo.jpg" must not collide.
/// </summary>
public class LocalPhotoStorage : IPhotoStorage
{
    private readonly string _webRoot;

    public LocalPhotoStorage(IHostEnvironment environment)
    {
        _webRoot = Path.Combine(environment.ContentRootPath, "wwwroot");
    }

    public async Task<string> SaveAsync(
        PhotoUpload upload,
        string folder,
        CancellationToken cancellationToken = default)
    {
        // The extension comes from the sniffed bytes, decided by the caller.
        var extension = Path.GetExtension(upload.FileName);
        var fileName = $"{Guid.NewGuid():N}{extension}";

        var directory = Path.Combine(_webRoot, folder);
        Directory.CreateDirectory(directory);

        var fullPath = Path.Combine(directory, fileName);

        await using (var file = File.Create(fullPath))
        {
            await upload.Content.CopyToAsync(file, cancellationToken);
        }

        // Forward slashes: this is a URL, not a filesystem path.
        return $"/{folder}/{fileName}";
    }

    public Task DeleteAsync(string url, CancellationToken cancellationToken = default)
    {
        // Resolve and confirm the result is still inside wwwroot before deleting anything.
        var relative = url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_webRoot, relative));

        if (fullPath.StartsWith(_webRoot, StringComparison.Ordinal) && File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }
}
