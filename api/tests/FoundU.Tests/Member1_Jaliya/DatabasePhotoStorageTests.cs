using FoundU.Api.Controllers;
using FoundU.Application.Abstractions;
using FoundU.Infrastructure.Persistence;
using FoundU.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Tests;

/// <summary>
/// Photos kept in the database, so a host that wipes its disk on every deploy keeps them.
/// </summary>
[Trait("Member", "Member1-Jaliya")]
public sealed class DatabasePhotoStorageTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5, 6, 7, 8];

    [Fact]
    public async Task APhotoIsSavedWithItsRowAndServedBack()
    {
        var name = Guid.NewGuid().ToString();
        string url;
        await using (var db = Context(name))
        {
            url = await new DatabasePhotoStorage(db).SaveAsync(
                new PhotoUpload(".jpg", "image/jpeg", Jpeg.Length, new MemoryStream(Jpeg)), "uploads/lost-reports/x");
            // Nothing is written until the caller's SaveChanges - the photo and its row go together.
            Assert.Empty(await Context(name).StoredPhotos.ToListAsync());
            await db.SaveChangesAsync();
        }

        Assert.Matches(@"^/api/photos/[0-9a-f]{32}\.jpg$", url);

        // A fresh context: what a later request - or a restarted server - would see.
        await using var later = Context(name);
        var result = await new PhotosController(later).Get(url["/api/photos/".Length..], CancellationToken.None);
        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("image/jpeg", file.ContentType);
        Assert.Equal(Jpeg, file.FileContents);
    }

    [Fact]
    public async Task AnUnknownOrMalformedNameIsNotFound()
    {
        await using var db = Context(Guid.NewGuid().ToString());
        var controller = new PhotosController(db);

        Assert.IsType<NotFoundResult>(await controller.Get($"{Guid.NewGuid():N}.jpg", CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Get("../../appsettings.json", CancellationToken.None));
    }

    [Fact]
    public async Task OnlyImageTypesAreStored()
    {
        await using var db = Context(Guid.NewGuid().ToString());
        await Assert.ThrowsAsync<InvalidOperationException>(() => new DatabasePhotoStorage(db).SaveAsync(
            new PhotoUpload(".exe", "application/octet-stream", 3, new MemoryStream([1, 2, 3])), "x"));
    }

    private static FoundUDbContext Context(string name) =>
        new(new DbContextOptionsBuilder<FoundUDbContext>().UseInMemoryDatabase(name).Options);
}
