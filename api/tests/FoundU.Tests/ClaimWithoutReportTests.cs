using FoundU.Application.Abstractions;
using FoundU.Application.Auth.Dtos;
using FoundU.Application.Claims.Dtos;
using FoundU.Application.Common.Exceptions;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FoundU.Tests;

/// <summary>
/// An owner who never reported the item lost can still get it back: by claiming it from the
/// item (staff then ask their questions on the claim), or in person at the desk.
/// </summary>
public sealed class ClaimWithoutReportTests
{
    private sealed record World(AuthTestApp App, Guid StaffId, Guid StudentId, FoundReport Item, IClaimService Claims);

    private static async Task<World> CreateAsync(FoundReportStatus status = FoundReportStatus.Unclaimed)
    {
        var app = await AuthTestApp.CreateAsync();
        var staff = await app.RegisterAsync("staff@foundu.test");
        (await app.Db.Users.SingleAsync(u => u.Id == staff.User.Id)).Role = UserRole.Staff;
        var student = await app.Auth.RegisterAsync(
            new RegisterRequest("Nadia Owner", "nadia@foundu.test", "Password123", "IT-2026-077"), null);

        var category = new Category { Name = "Bags" };
        var itemType = new ItemType { Name = "Wallet", Category = category };
        var location = new CampusLocation { Name = "Library" };
        var storage = new StorageLocation { Name = "Main desk" };
        var item = new FoundReport
        {
            StaffId = staff.User.Id, Category = category, ItemType = itemType, FoundLocation = location,
            StorageLocation = storage, GeneralDescription = "Yellow wallet", PrimaryColor = "Yellow",
            PrivateVerificationDetails = "A library card for N. Owner inside", FoundAt = DateTime.UtcNow.AddHours(-2),
            Status = status,
        };
        app.Db.AddRange(category, itemType, location, storage, item);
        await app.Db.SaveChangesAsync();
        return new World(app, staff.User.Id, student.User.Id, item, app.Services.GetRequiredService<IClaimService>());
    }

    [Fact]
    public async Task AStudentWithNoReportClaimsFromTheItemAndTheClaimGoesToTheDesk()            // normal
    {
        var w = await CreateAsync();
        await using var _ = w.App;

        var claim = await w.Claims.ClaimWithoutReportAsync(
            new ClaimWithoutReportRequest(w.Item.Id, "Yellow leather wallet, my library card is inside"), w.StudentId);

        Assert.Equal(nameof(ClaimStatus.Pending), claim.Status);
        Assert.Equal(w.Item.Id, claim.FoundItem.Id);
        var report = await w.App.Db.LostReports.SingleAsync(r => r.Id == claim.LostReportId);
        Assert.True(report.CreatedForClaim);
        Assert.Equal(w.StudentId, report.StudentId);
        Assert.Equal(LostReportStatus.Matched, report.Status);          // never on the public feed
        Assert.Equal("Yellow leather wallet, my library card is inside", report.Description);
        Assert.Contains((await w.Claims.SearchAsync(new ClaimQuery())).Items, c => c.Id == claim.Id);

        // Asking again returns the same claim rather than a second one.
        var again = await w.Claims.ClaimWithoutReportAsync(new ClaimWithoutReportRequest(w.Item.Id, "Same wallet again, please"), w.StudentId);
        Assert.Equal(claim.Id, again.Id);
    }

    [Fact]
    public async Task AnItemStillWithItsFinderCannotBeClaimedYet()                               // invalid
    {
        var w = await CreateAsync(FoundReportStatus.Posted);
        await using var _ = w.App;

        await Assert.ThrowsAsync<ConflictAppException>(() => w.Claims.ClaimWithoutReportAsync(
            new ClaimWithoutReportRequest(w.Item.Id, "Yellow wallet with my card"), w.StudentId));
        Assert.False(await w.App.Db.LostReports.AnyAsync());
    }

    [Fact]
    public async Task WhenTheClaimIsRejectedTheReportMadeForItIsWithdrawnNotPutOnTheFeed()       // failure
    {
        var w = await CreateAsync();
        await using var _ = w.App;
        var claim = await w.Claims.ClaimWithoutReportAsync(
            new ClaimWithoutReportRequest(w.Item.Id, "Yellow wallet with my card"), w.StudentId);

        await w.Claims.DecideAsync(claim.Id, w.StaffId, new ClaimDecisionRequest("Rejected", "The details did not match."));

        var report = await w.App.Db.LostReports.SingleAsync(r => r.Id == claim.LostReportId);
        Assert.Equal(LostReportStatus.Withdrawn, report.Status);
    }

    [Fact]
    public async Task StaffWriteTheirOwnQuestionsButNeverGiveTheHiddenDetailAway()              // normal + failure
    {
        var w = await CreateAsync();
        await using var _ = w.App;
        var claim = await w.Claims.ClaimWithoutReportAsync(
            new ClaimWithoutReportRequest(w.Item.Id, "Yellow wallet with my card inside"), w.StudentId);

        await Assert.ThrowsAsync<ValidationAppException>(() => w.Claims.AddQuestionsAsync(
            claim.Id, w.StaffId, new AddVerificationQuestionsRequest(["Is there a library card for N. Owner in it?"])));

        var asked = await w.Claims.AddQuestionsAsync(
            claim.Id, w.StaffId, new AddVerificationQuestionsRequest(["What is inside the wallet?"]));
        Assert.Equal("What is inside the wallet?", Assert.Single(asked.Questions).QuestionText);
    }

    // ----------------------------------------------------------------- in person at the desk

    [Fact]
    public async Task StaffVerifyInPersonAndHandOverInOneStep()                                   // normal
    {
        var w = await CreateAsync();
        await using var _ = w.App;

        var done = await w.Claims.HandOverInPersonAsync(new InPersonHandoverRequest(
            w.Item.Id, w.StudentId, "Asked what is inside - said her library card, name matches.", OwnerIdChecked: true), w.StaffId);

        Assert.Equal(nameof(ClaimStatus.Approved), done.Status);
        Assert.NotNull(done.CollectedAt);
        Assert.Null(done.CollectionCode);
        var db = w.App.Db;
        Assert.Equal(FoundReportStatus.Returned, (await db.FoundReports.SingleAsync()).Status);
        Assert.Equal(LostReportStatus.Resolved, (await db.LostReports.SingleAsync()).Status);

        // The notes stay with staff; the owner's decision only says how it was verified.
        Assert.Contains(await db.ClaimStatusHistories.Where(h => h.ClaimId == done.Id).Select(h => h.Reason).ToListAsync(),
            r => r != null && r.Contains("library card"));
        Assert.Equal("Verified in person at the security desk.", (await db.ApprovalDecisions.SingleAsync()).Reason);

        // A receipt, but no "go and collect it with this code" - they already have it.
        var notes = await db.Notifications.Where(n => n.UserId == w.StudentId).Select(n => n.Type).ToListAsync();
        Assert.Contains(NotificationType.ItemCollected, notes);
        Assert.DoesNotContain(NotificationType.CollectionInstructions, notes);
    }

    [Fact]
    public async Task AnInPersonHandOverNeedsTheIdCheckAStudentAndAnItemAtTheDesk()               // invalid
    {
        var w = await CreateAsync();
        await using var _ = w.App;

        await Assert.ThrowsAsync<ValidationAppException>(() => w.Claims.HandOverInPersonAsync(
            new InPersonHandoverRequest(w.Item.Id, w.StudentId, "Asked about the card inside."), w.StaffId));
        await Assert.ThrowsAsync<ValidationAppException>(() => w.Claims.HandOverInPersonAsync(
            new InPersonHandoverRequest(w.Item.Id, w.StaffId, "Asked about the card inside.", OwnerIdChecked: true), w.StaffId));
        await Assert.ThrowsAsync<NotFoundAppException>(() => w.Claims.HandOverInPersonAsync(
            new InPersonHandoverRequest(w.Item.Id, Guid.NewGuid(), "Asked about the card inside.", OwnerIdChecked: true), w.StaffId));
        Assert.Equal(FoundReportStatus.Unclaimed, (await w.App.Db.FoundReports.SingleAsync()).Status);
    }

    [Fact]
    public async Task TheDeskFindsStudentsByEmailOrStudentNumberAndNeverStaff()                   // boundary
    {
        var w = await CreateAsync();
        await using var _ = w.App;

        Assert.Single(await w.Claims.FindStudentsAsync("nadia@foundu"));
        Assert.Single(await w.Claims.FindStudentsAsync("IT-2026-077"));
        Assert.Empty(await w.Claims.FindStudentsAsync("staff@foundu"));
        Assert.Empty(await w.Claims.FindStudentsAsync("n"));                // one letter is not a search
    }
}
