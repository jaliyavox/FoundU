using FoundU.Application.Common.Exceptions;
using FoundU.Application.Support.Dtos;
using FoundU.Domain.Entities;
using FoundU.Domain.Enums;
using FoundU.Infrastructure.Notifications;
using FoundU.Infrastructure.Persistence;
using FoundU.Infrastructure.Support;
using Microsoft.EntityFrameworkCore;

namespace FoundU.Tests;

/// <summary>
/// Support tickets. The rules worth pinning: a ticket belongs to the person who raised it,
/// staff answers move it out of the queue, a reply from the person brings it back, and a
/// closed ticket stays closed.
/// </summary>
public sealed class SupportTicketTests
{
    [Fact]
    public async Task OpeningATicketPutsItInTheQueueWithItsFirstMessage()
    {
        await using var fixture = await Fixture.CreateAsync();

        var ticket = await fixture.Service.CreateAsync(
            new CreateSupportTicketRequest("Cannot collect my bag", "Collection", "The desk says my code is used.", null, null),
            fixture.Student.Id);

        Assert.Equal("Open", ticket.Status);
        Assert.Equal("Collection", ticket.Category);
        Assert.Equal(fixture.Student.Id, ticket.RaisedById);
        var message = Assert.Single(ticket.Messages);
        Assert.False(message.IsStaffReply);
        Assert.Equal("The desk says my code is used.", message.Body);

        var stats = await fixture.Service.GetQueueStatsAsync();
        Assert.Equal(1, stats.Open);
        Assert.Equal(1, stats.Unassigned);
    }

    [Theory]
    [InlineData("42")]
    [InlineData("Nonsense")]
    public void ANumberOrUnknownNameIsNotACategoryOrStatus(string value)
    {
        var create = new FoundU.Application.Support.Validators.CreateSupportTicketRequestValidator()
            .Validate(new CreateSupportTicketRequest("Cannot collect my bag", value, "The desk says my code is used.", null, null));
        var update = new FoundU.Application.Support.Validators.UpdateSupportTicketRequestValidator()
            .Validate(new UpdateSupportTicketRequest(value, null));

        Assert.False(create.IsValid);
        Assert.False(update.IsValid);
    }

    [Fact]
    public async Task AStudentCannotReadOrWriteOnSomebodyElsesTicket()
    {
        await using var fixture = await Fixture.CreateAsync();
        var ticket = await fixture.OpenAsync();

        await Assert.ThrowsAsync<ForbiddenAppException>(
            () => fixture.Service.GetByIdAsync(ticket.Id, fixture.Other.Id, isStaff: false));
        await Assert.ThrowsAsync<ForbiddenAppException>(
            () => fixture.Service.ReplyAsync(ticket.Id, fixture.Other.Id, isStaff: false, "let me in"));

        // Staff may read any ticket - that is the point of a desk.
        var staffView = await fixture.Service.GetByIdAsync(ticket.Id, fixture.Staff.Id, isStaff: true);
        Assert.Equal(ticket.Id, staffView.Id);
    }

    [Fact]
    public async Task AStaffReplyWaitsOnThePersonAndTellsThem()
    {
        await using var fixture = await Fixture.CreateAsync();
        var ticket = await fixture.OpenAsync();

        var answered = await fixture.Service.ReplyAsync(ticket.Id, fixture.Staff.Id, isStaff: true, "Bring your student ID to the library desk.");

        Assert.Equal("Waiting", answered.Status);
        // Answering an unassigned ticket picks it up, so two people do not both work it.
        Assert.Equal(fixture.Staff.Id, answered.AssignedToUserId);
        Assert.True(answered.Messages.Last().IsStaffReply);

        var notification = await fixture.Db.Notifications.SingleAsync(n => n.UserId == fixture.Student.Id);
        Assert.Equal(NotificationType.SupportTicketReply, notification.Type);

        // And the person writing back brings it straight back into the queue.
        var reopened = await fixture.Service.ReplyAsync(ticket.Id, fixture.Student.Id, isStaff: false, "I did, they turned me away.");
        Assert.Equal("Open", reopened.Status);
        Assert.False(reopened.Messages.Last().IsStaffReply);
    }

    [Fact]
    public async Task ResolvingTellsThePersonAndWritingAgainReopensIt()
    {
        await using var fixture = await Fixture.CreateAsync();
        var ticket = await fixture.OpenAsync();

        var resolved = await fixture.Service.UpdateAsync(
            ticket.Id, fixture.Staff.Id, new UpdateSupportTicketRequest("Resolved", fixture.Staff.Id));
        Assert.Equal("Resolved", resolved.Status);
        Assert.NotNull(resolved.ResolvedAt);
        Assert.Contains(
            await fixture.Db.Notifications.Where(n => n.UserId == fixture.Student.Id).ToListAsync(),
            n => n.Type == NotificationType.SupportTicketUpdated);

        var reopened = await fixture.Service.ReplyAsync(ticket.Id, fixture.Student.Id, isStaff: false, "It happened again.");
        Assert.Equal("Open", reopened.Status);
        Assert.Null(reopened.ResolvedAt);
    }

    [Fact]
    public async Task AClosedTicketTakesNoMoreMessages()
    {
        await using var fixture = await Fixture.CreateAsync();
        var ticket = await fixture.OpenAsync();
        await fixture.Service.UpdateAsync(ticket.Id, fixture.Staff.Id, new UpdateSupportTicketRequest("Closed", null));

        await Assert.ThrowsAsync<ConflictAppException>(
            () => fixture.Service.ReplyAsync(ticket.Id, fixture.Student.Id, isStaff: false, "still broken"));
    }

    [Fact]
    public async Task TicketsAreOnlyAssignedToStaff()
    {
        await using var fixture = await Fixture.CreateAsync();
        var ticket = await fixture.OpenAsync();

        await Assert.ThrowsAsync<ValidationAppException>(() => fixture.Service.UpdateAsync(
            ticket.Id, fixture.Staff.Id, new UpdateSupportTicketRequest("Open", fixture.Other.Id)));
    }

    [Fact]
    public async Task EachSideSeesOnlyTheOtherSidesMessagesAsUnread()
    {
        await using var fixture = await Fixture.CreateAsync();
        var ticket = await fixture.OpenAsync();

        // The desk has one unread - the question. The student has none: they wrote it.
        var queue = await fixture.Service.SearchAsync(fixture.Staff.Id, new SupportTicketQuery());
        Assert.Equal(1, queue.Items.Single().UnreadCount);
        var mine = await fixture.Service.GetMineAsync(fixture.Student.Id, new SupportTicketQuery());
        Assert.Equal(0, mine.Items.Single().UnreadCount);

        // Reading it as staff clears their side, and an answer lands unread for the student.
        await fixture.Service.GetByIdAsync(ticket.Id, fixture.Staff.Id, isStaff: true);
        await fixture.Service.ReplyAsync(ticket.Id, fixture.Staff.Id, isStaff: true, "Come by the desk.");

        queue = await fixture.Service.SearchAsync(fixture.Staff.Id, new SupportTicketQuery());
        Assert.Equal(0, queue.Items.Single().UnreadCount);
        mine = await fixture.Service.GetMineAsync(fixture.Student.Id, new SupportTicketQuery());
        Assert.Equal(1, mine.Items.Single().UnreadCount);
    }

    [Fact]
    public async Task OnlyStaffSeeTheEmailAddressOfWhoeverRaisedIt()
    {
        await using var fixture = await Fixture.CreateAsync();
        var ticket = await fixture.OpenAsync();

        var ownerView = await fixture.Service.GetByIdAsync(ticket.Id, fixture.Student.Id, isStaff: false);
        Assert.Null(ownerView.RaisedByEmail);

        var staffView = await fixture.Service.GetByIdAsync(ticket.Id, fixture.Staff.Id, isStaff: true);
        Assert.Equal("student@test", staffView.RaisedByEmail);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(FoundUDbContext db, SupportService service, AppUser student, AppUser staff, AppUser other)
            => (Db, Service, Student, Staff, Other) = (db, service, student, staff, other);

        public FoundUDbContext Db { get; }
        public SupportService Service { get; }
        public AppUser Student { get; }
        public AppUser Staff { get; }
        public AppUser Other { get; }

        public Task<SupportTicketDetailDto> OpenAsync() => Service.CreateAsync(
            new CreateSupportTicketRequest("Cannot collect my bag", "Collection", "The desk says my code is used.", null, null),
            Student.Id);

        public static async Task<Fixture> CreateAsync()
        {
            var db = new FoundUDbContext(new DbContextOptionsBuilder<FoundUDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

            var student = new AppUser { FullName = "Student One", UserName = "student@test", Email = "student@test", Role = UserRole.Student };
            var staff = new AppUser { FullName = "Desk Staff", UserName = "staff@test", Email = "staff@test", Role = UserRole.Staff };
            var other = new AppUser { FullName = "Student Two", UserName = "other@test", Email = "other@test", Role = UserRole.Student };
            db.AddRange(student, staff, other);
            await db.SaveChangesAsync();

            return new Fixture(db, new SupportService(db, new NotificationService(db)), student, staff, other);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
