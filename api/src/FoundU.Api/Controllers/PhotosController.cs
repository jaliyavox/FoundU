using FoundU.Infrastructure.Persistence;
using FoundU.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Api.Controllers;

/// <summary>
/// Serves uploaded report photos. Public, like the feed that shows them - they are the same
/// photos the lost feed already displays to anyone. Each id is random and never reused, so a
/// browser may keep a photo for a year.
/// </summary>
[ApiController]
[Route("api/photos")]
[AllowAnonymous]
public class PhotosController : ControllerBase
{
    private readonly FoundUDbContext _db;

    public PhotosController(FoundUDbContext db)
    {
        _db = db;
    }

    [HttpGet("{name}")]
    [ResponseCache(Duration = 31536000, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Get(string name, CancellationToken cancellationToken)
    {
        if (!DatabasePhotoStorage.TryReadId(name, out var id)) return NotFound();

        var photo = await _db.StoredPhotos.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new { p.ContentType, p.Data })
            .FirstOrDefaultAsync(cancellationToken);

        return photo is null ? NotFound() : File(photo.Data, photo.ContentType);
    }
}
