using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FoundU.Application.Abstractions;
using FoundU.Application.Auth.Dtos;
using FoundU.Application.Common.Exceptions;
using FoundU.Application.Support.Dtos;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FoundU.Tests;

/// <summary>
/// An administrator deleting support tickets and user accounts. A deletion is soft - the rows
/// stay for history - but the person's details are erased, their open work is closed the way
/// they would close it, and they can never sign in again.
/// </summary>
[Trait("Member", "Member1-Jaliya")]
public sealed class AdminDeletionTests
{
    private sealed record World(
        AuthTestApp App, Guid AdminId, Guid StaffId, Guid StudentId,
        Category Category, ItemType ItemType, CampusLocation Location, StorageLocation Storage);

    private static async Task<World> CreateAsync()
    {
        var app = await AuthTestApp.CreateAsync();
        var admin = await app.RegisterAsync("admin@foundu.test");
        var staff = await app.RegisterAsync("staff@foundu.test");
        var student = await app.Auth.RegisterAsync(
            new RegisterRequest("Nadia Student", "nadia@foundu.test", "Password123", "IT-2026-001"), null);
        (await app.Db.Users.SingleAsync(u => u.Id == admin.User.Id)).Role = UserRole.Admin;
        (await app.Db.Users.SingleAsync(u => u.Id == staff.User.Id)).Role = UserRole.Staff;

        var category = new Category { Name = "Bags" };
        var itemType = new ItemType { Name = "Backpack", Category = category };
        var location = new CampusLocation { Name = "Library" };
        var storage = new StorageLocation { Name = "Main desk" };
        app.Db.AddRange(category, itemType, location, storage);
        await app.Db.SaveChangesAsync();
        return new World(app, admin.User.Id, staff.User.Id, student.User.Id, category, itemType, location, storage);
    }

    private static LostReport Report(World w, Guid studentId) => new()
    {
        StudentId = studentId, CategoryId = w.Category.Id, ItemTypeId = w.ItemType.Id,
        LastSeenLocationId = w.Location.Id, Description = "Black backpack with two zips",
        EstimatedLostFromAt = DateTime.UtcNow.AddHours(-3), EstimatedLostToAt = DateTime.UtcNow.AddHours(-1),
        Status = LostReportStatus.Active,
    };

    private static FoundReport Item(World w, FoundReportStatus status, Guid? finderId = null) => new()
    {
        StaffId = finderId is null ? w.StaffId : null, FinderId = finderId,
        CategoryId = w.Category.Id, ItemTypeId = w.ItemType.Id, FoundLocationId = w.Location.Id,
        StorageLocationId = finderId is null ? w.Storage.Id : null,
        GeneralDescription = "Black backpack", PrivateVerificationDetails = "a name tag inside",
        FoundAt = DateTime.UtcNow, Status = status,
    };

    // ------------------------------------------------------------------ users

    [Fact]
    public async Task DeletingAStudentClosesTheirWorkErasesTheirDetailsAndEndsEverySession()   // normal
    {
        var w = await CreateAsync();
        await using var _ = w.App;
        var report = Report(w, w.StudentId);
        var post = Item(w, FoundReportStatus.Posted, finderId: w.StudentId);
        w.App.Db.AddRange(report, post);
        await w.App.Db.SaveChangesAsync();
        var support = w.App.Services.GetRequiredService<ISupportService>();
        var ticket = await support.CreateAsync(
            new CreateSupportTicketRequest("Wrong item", "Account", "Please close my account and remove my details.", null, null),
            w.StudentId);

        await w.App.Services.GetRequiredService<IAccountDeletionService>().DeleteAsync(w.StudentId, w.AdminId);

        var db = w.App.Db;
        Assert.False(await db.Users.AnyAsync(u => u.Id == w.StudentId));        // gone from every query
        var saved = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == w.StudentId);
        Assert.True(saved.IsDeleted);
        Assert.Equal("Deleted user", saved.FullName);
        Assert.DoesNotContain("nadia", saved.Email);
        Assert.Null(saved.StudentNumber);
        Assert.Null(saved.PasswordHash);
        Assert.All(await db.RefreshTokens.Where(t => t.UserId == w.StudentId).ToListAsync(), t => Assert.NotNull(t.RevokedAt));

        Assert.Equal(LostReportStatus.Withdrawn, (await db.LostReports.IgnoreQueryFilters().SingleAsync(r => r.Id == report.Id)).Status);
        Assert.Equal(FoundReportStatus.Disposed, (await db.FoundReports.IgnoreQueryFilters().SingleAsync(p => p.Id == post.Id)).Status);
        Assert.False(await db.SupportTickets.AnyAsync(t => t.Id == ticket.Id));

        await Assert.ThrowsAsync<UnauthorizedAppException>(
            () => w.App.Auth.LoginAsync(new LoginRequest("nadia@foundu.test", "Password123"), null));
    }

    [Fact]
    public async Task ADeletedPersonCanRegisterAgainWithTheSameEmailAndStudentNumber()           // boundary
    {
        var w = await CreateAsync();
        await using var _ = w.App;

        await w.App.Services.GetRequiredService<IAccountDeletionService>().DeleteAsync(w.StudentId, w.AdminId);
        var again = await w.App.Auth.RegisterAsync(
            new RegisterRequest("Nadia Student", "nadia@foundu.test", "Password123", "IT-2026-001"), null);

        Assert.NotEqual(w.StudentId, again.User.Id);
    }

    [Fact]
    public async Task AnAdminCannotDeleteThemselvesOrAnotherAdmin()                               // invalid
    {
        var w = await CreateAsync();
        await using var _ = w.App;
        var accounts = w.App.Services.GetRequiredService<IAccountDeletionService>();
        var other = await w.App.RegisterAsync("admin2@foundu.test");
        (await w.App.Db.Users.SingleAsync(u => u.Id == other.User.Id)).Role = UserRole.Admin;
        await w.App.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<ValidationAppException>(() => accounts.DeleteAsync(w.AdminId, w.AdminId));
        await Assert.ThrowsAsync<ForbiddenAppException>(() => accounts.DeleteAsync(other.User.Id, w.AdminId));
        await Assert.ThrowsAsync<NotFoundAppException>(() => accounts.DeleteAsync(Guid.NewGuid(), w.AdminId));
    }

    [Fact]
    public async Task AStudentWithAnItemWaitingAtTheDeskIsNotDeletedAndNothingChanges()          // failure
    {
        var w = await CreateAsync();
        await using var _ = w.App;
        var report = Report(w, w.StudentId);
        report.Status = LostReportStatus.Matched;
        var item = Item(w, FoundReportStatus.Claimed);
        w.App.Db.AddRange(report, item);
        w.App.Db.Claims.Add(new Claim
        {
            StudentId = w.StudentId, LostReport = report, FoundReport = item, Status = ClaimStatus.Approved,
        });
        await w.App.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictAppException>(
            () => w.App.Services.GetRequiredService<IAccountDeletionService>().DeleteAsync(w.StudentId, w.AdminId));

        var saved = await w.App.Db.Users.SingleAsync(u => u.Id == w.StudentId);
        Assert.Equal("Nadia Student", saved.FullName);
        Assert.Equal(LostReportStatus.Matched, (await w.App.Db.LostReports.SingleAsync(r => r.Id == report.Id)).Status);
    }

    [Fact]
    public async Task StaffWhoDecidedClaimsAreKeptOnRecordAndMustBeSuspendedInstead()            // failure
    {
        var w = await CreateAsync();
        await using var _ = w.App;
        var report = Report(w, w.StudentId);
        var item = Item(w, FoundReportStatus.Unclaimed);
        var claim = new Claim { StudentId = w.StudentId, LostReport = report, FoundReport = item, Status = ClaimStatus.Rejected };
        w.App.Db.AddRange(report, item, claim);
        w.App.Db.ApprovalDecisions.Add(new ApprovalDecision
        {
            Claim = claim, DecidedByUserId = w.StaffId, Decision = ApprovalDecisionType.Rejected, Reason = "Answers did not match.",
        });
        await w.App.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictAppException>(
            () => w.App.Services.GetRequiredService<IAccountDeletionService>().DeleteAsync(w.StaffId, w.AdminId));
        Assert.True(await w.App.Db.Users.AnyAsync(u => u.Id == w.StaffId));
    }

    // ------------------------------------------------------------------ tickets

    [Fact]
    public async Task ADeletedTicketLeavesTheQueueAndTheStudentsList()                           // normal
    {
        var w = await CreateAsync();
        await using var _ = w.App;
        var support = w.App.Services.GetRequiredService<ISupportService>();
        var ticket = await support.CreateAsync(
            new CreateSupportTicketRequest("Spam", "Other", "Buy cheap watches at example dot com", null, null), w.StudentId);

        await support.DeleteAsync(ticket.Id);

        Assert.Empty((await support.SearchAsync(w.StaffId, new SupportTicketQuery())).Items);
        Assert.Empty((await support.GetMineAsync(w.StudentId, new SupportTicketQuery())).Items);
        await Assert.ThrowsAsync<NotFoundAppException>(() => support.GetByIdAsync(ticket.Id, w.StudentId, isStaff: false));
        await Assert.ThrowsAsync<NotFoundAppException>(() => support.DeleteAsync(ticket.Id));      // twice: already gone
    }
}

/// <summary>Only administrators reach the delete endpoints; staff and students get 403.</summary>
[Trait("Member", "Member1-Jaliya")]
public sealed class AdminDeletionEndpointTests : IClassFixture<FoundUWebApplicationFactory>
{
    private readonly FoundUWebApplicationFactory _factory;
    public AdminDeletionEndpointTests(FoundUWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/api/admin/users/{0}")]
    [InlineData("/api/admin/support/tickets/{0}")]
    public async Task AStudentCannotDeleteUsersOrTickets(string route)                            // invalid
    {
        using var client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var registration = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Curious Student", $"del-{Guid.NewGuid():N}@foundu.test", "Password123", null));
        var auth = await registration.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await client.DeleteAsync(string.Format(route, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
