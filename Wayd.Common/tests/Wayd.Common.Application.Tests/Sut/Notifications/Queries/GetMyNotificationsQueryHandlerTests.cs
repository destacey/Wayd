using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Notifications.Queries;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Following;
using Wayd.Common.Domain.Notifications;
using Wayd.Common.Domain.Tests.Data;

namespace Wayd.Common.Application.Tests.Sut.Notifications.Queries;

public sealed class GetMyNotificationsQueryHandlerTests
{
    private const string GadgetType = "test.my-notifications.gadget";
    private const string WidgetType = "test.my-notifications.widget";
    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Test My-Notifications Gadget");
    private static readonly FollowOwnerDescriptor Widget = new(WidgetType, "Test My-Notifications Widget");
    private static readonly Instant Start = Instant.FromUtc(2026, 10, 1, 9, 0);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly FakeRecordReadRule _gadgets = new(GadgetType);
    private readonly FakeRecordReadRule _widgets = new(WidgetType);
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Guid _employeeId = Guid.NewGuid();
    private long _nextId = 1;

    public GetMyNotificationsQueryHandlerTests()
    {
        FollowOwners.Register(Gadget, Widget);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
    }

    private GetMyNotificationsQueryHandler CreateHandler() =>
        new(_dbContext, _dbContext, [_widgets, _gadgets], _currentPrincipal.Object);

    [Fact]
    public async Task Handle_ShouldCollapseEachRecordsNotificationsIntoOneEntry_NewestChangeFirst()
    {
        // Arrange
        var sprocket = _gadgets.Add("G-1", "Sprocket");
        var lever = _widgets.Add("W-1", "Lever");
        Seed(GadgetType, sprocket.Id, hours: 1, NotificationEvent.StatusChanged, NotificationReason.Role, read: true);
        Seed(GadgetType, sprocket.Id, hours: 5, NotificationEvent.HealthReported, NotificationReason.Following);
        Seed(WidgetType, lever.Id, hours: 3, NotificationEvent.TimelineChanged, NotificationReason.Following);

        // Act
        var result = await CreateHandler().Handle(new GetMyNotificationsQuery(new CursorRequest()), TestContext.Current.CancellationToken);

        // Assert
        result.Items.Select(i => i.Record.Key).Should().Equal("G-1", "W-1");
        result.NextCursor.Should().BeNull();

        var entry = result.Items[0];
        entry.Record.OwnerType.Should().Be(GadgetType);
        entry.Record.Id.Should().Be(sprocket.Id);
        entry.Record.Name.Should().Be("Sprocket");
        entry.Event.Should().Be(NotificationEvent.HealthReported);
        entry.OccurredOn.Should().Be(Start.Plus(Duration.FromHours(5)));
        entry.Count.Should().Be(2);
        entry.UnreadCount.Should().Be(1);
        entry.IsUnread.Should().BeTrue();
        entry.Reasons.Should().BeEquivalentTo([NotificationReason.Following, NotificationReason.Role]);
    }

    [Theory]
    [InlineData(NotificationReason.Following)]
    [InlineData(NotificationReason.Requester)]
    [InlineData(NotificationReason.Participant)]
    [InlineData(NotificationReason.Assigned)]
    [InlineData(NotificationReason.Mentioned)]
    [InlineData(NotificationReason.Desk)]
    [InlineData(NotificationReason.Role)]
    public async Task Handle_ShouldKeepEveryReason_WhenCollapsingARecordsNotifications(NotificationReason reason)
    {
        // Arrange
        var sprocket = _gadgets.Add("G-1", "Sprocket");
        Seed(GadgetType, sprocket.Id, hours: 1, NotificationEvent.StatusChanged, reason);

        // Act
        var result = await CreateHandler().Handle(new GetMyNotificationsQuery(new CursorRequest()), TestContext.Current.CancellationToken);

        // Assert
        result.Items.Should().ContainSingle()
            .Which.Reasons.Should().Equal(reason);
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutRecordsTheCallerCanNoLongerReadOrThatNoLongerExist()
    {
        // Arrange
        var visible = _gadgets.Add("G-1", "Visible");
        var hidden = _gadgets.Add("G-2", "Hidden");
        var deleted = _gadgets.Add("G-3", "Deleted");
        Seed(GadgetType, visible.Id, hours: 1);
        Seed(GadgetType, hidden.Id, hours: 2);
        Seed(GadgetType, deleted.Id, hours: 3);
        _gadgets.Hide(hidden.Id);
        _gadgets.Delete(deleted.Id);

        // Act
        var result = await CreateHandler().Handle(new GetMyNotificationsQuery(new CursorRequest()), TestContext.Current.CancellationToken);

        // Assert
        result.Items.Should().ContainSingle().Which.Record.Key.Should().Be("G-1");
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutEveryRecordOfATypeTheCallerCannotRead()
    {
        // Arrange
        var sprocket = _gadgets.Add("G-1", "Sprocket");
        Seed(GadgetType, sprocket.Id, hours: 1);
        _gadgets.CanView = false;

        // Act
        var result = await CreateHandler().Handle(new GetMyNotificationsQuery(new CursorRequest()), TestContext.Current.CancellationToken);

        // Assert
        result.Items.Should().BeEmpty();
        result.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldListOnlyTheCallersNotifications()
    {
        // Arrange
        var sprocket = _gadgets.Add("G-1", "Sprocket");
        Seed(GadgetType, sprocket.Id, hours: 1, recipientId: Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(new GetMyNotificationsQuery(new CursorRequest()), TestContext.Current.CancellationToken);

        // Assert
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldKeepOnlyEntriesWithSomethingUnread_WhenAsked()
    {
        // Arrange
        var read = _gadgets.Add("G-1", "Read");
        var unread = _gadgets.Add("G-2", "Unread");
        Seed(GadgetType, read.Id, hours: 1, read: true);
        Seed(GadgetType, unread.Id, hours: 2);

        // Act
        var result = await CreateHandler().Handle(new GetMyNotificationsQuery(new CursorRequest(), UnreadOnly: true), TestContext.Current.CancellationToken);

        // Assert
        result.Items.Should().ContainSingle().Which.Record.Key.Should().Be("G-2");
    }

    [Fact]
    public async Task Handle_ShouldPageByCursor_UntilTheLastPageHasNoCursor()
    {
        // Arrange
        for (var i = 1; i <= 3; i++)
        {
            var record = _gadgets.Add($"G-{i}", $"Gadget {i}");
            Seed(GadgetType, record.Id, hours: i);
        }

        // Act
        var first = await CreateHandler().Handle(new GetMyNotificationsQuery(new CursorRequest(PageSize: 2)), TestContext.Current.CancellationToken);
        var second = await CreateHandler().Handle(new GetMyNotificationsQuery(new CursorRequest(Before: first.NextCursor, PageSize: 2)), TestContext.Current.CancellationToken);

        // Assert
        first.Items.Select(i => i.Record.Key).Should().Equal("G-3", "G-2");
        first.NextCursor.Should().NotBeNull();
        second.Items.Select(i => i.Record.Key).Should().Equal("G-1");
        second.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldGiveNoCursor_WhenThePageEndsExactlyAtTheLastEntry()
    {
        // Arrange
        for (var i = 1; i <= 2; i++)
        {
            var record = _gadgets.Add($"G-{i}", $"Gadget {i}");
            Seed(GadgetType, record.Id, hours: i);
        }

        // Act
        var page = await CreateHandler().Handle(new GetMyNotificationsQuery(new CursorRequest(PageSize: 2)), TestContext.Current.CancellationToken);

        // Assert
        page.Items.Should().HaveCount(2);
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldNeitherRepeatNorSkipAnEntry_WhenOneMovesToTheTopBetweenPages()
    {
        // Arrange — four records, newest first G-4..G-1; the first page shows G-4 and G-3.
        var records = Enumerable.Range(1, 4).Select(i => _gadgets.Add($"G-{i}", $"Gadget {i}")).ToList();
        for (var i = 0; i < records.Count; i++)
        {
            Seed(GadgetType, records[i].Id, hours: i + 1);
        }

        var first = await CreateHandler().Handle(new GetMyNotificationsQuery(new CursorRequest(PageSize: 2)), TestContext.Current.CancellationToken);

        // G-1 changes while the first page is on screen, moving to the top of the list.
        Seed(GadgetType, records[0].Id, hours: 10);

        // Act
        var second = await CreateHandler().Handle(new GetMyNotificationsQuery(new CursorRequest(Before: first.NextCursor, PageSize: 2)), TestContext.Current.CancellationToken);

        // Assert — an offset would now give G-3 again and G-2; the cursor gives G-2 only, and G-1 waits on page one.
        first.Items.Select(i => i.Record.Key).Should().Equal("G-4", "G-3");
        second.Items.Select(i => i.Record.Key).Should().Equal("G-2");
        second.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldNameWhoMadeTheNewestChange()
    {
        // Arrange
        var actor = new EmployeeFaker().Generate();
        _dbContext.AddEmployee(actor);
        var sprocket = _gadgets.Add("G-1", "Sprocket");
        Seed(GadgetType, sprocket.Id, hours: 1, actorEmployeeId: actor.Id);

        // Act
        var result = await CreateHandler().Handle(new GetMyNotificationsQuery(new CursorRequest()), TestContext.Current.CancellationToken);

        // Assert
        var entry = result.Items.Should().ContainSingle().Subject;
        entry.ActorKind.Should().Be(EventActorKind.User);
        entry.Actor.Should().NotBeNull();
        entry.Actor!.Id.Should().Be(actor.Id);
        entry.Actor.Name.Should().Be(actor.Name.DisplayName);
    }

    [Fact]
    public async Task Handle_ShouldKeepRecordsOfDifferentTypesApart_WhenTheyShareAnId()
    {
        // Arrange — a replicated record keeps its source's id, so two owner types can share one.
        var sharedId = Guid.NewGuid();
        _gadgets.Add("G-1", "Sprocket", id: sharedId);
        _widgets.Add("W-1", "Lever", id: sharedId);
        Seed(GadgetType, sharedId, hours: 1, NotificationEvent.HealthReported, NotificationReason.Role);
        Seed(WidgetType, sharedId, hours: 2, NotificationEvent.TimelineChanged, NotificationReason.Following);

        // Act
        var result = await CreateHandler().Handle(new GetMyNotificationsQuery(new CursorRequest()), TestContext.Current.CancellationToken);

        // Assert
        result.Items.Select(i => (i.Record.Key, i.Event, i.Count)).Should().Equal(
            ("W-1", NotificationEvent.TimelineChanged, 1),
            ("G-1", NotificationEvent.HealthReported, 1));
        result.Items[1].Reasons.Should().Equal(NotificationReason.Role);
    }

    [Fact]
    public async Task Handle_ShouldOrderEntriesChangedAtTheSameInstantByTheirRecord_AcrossPages()
    {
        // Arrange — three records changed at one instant, across two owner types.
        var gadgetLow = _gadgets.Add("G-1", "Gadget low", id: Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var gadgetHigh = _gadgets.Add("G-2", "Gadget high", id: Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var widget = _widgets.Add("W-1", "Widget", id: Guid.Parse("00000000-0000-0000-0000-000000000000"));
        Seed(WidgetType, widget.Id, hours: 1);
        Seed(GadgetType, gadgetHigh.Id, hours: 1);
        Seed(GadgetType, gadgetLow.Id, hours: 1);

        // Act
        var keys = await WalkByCursor(new GetMyNotificationsQuery(new CursorRequest(PageSize: 1)));

        // Assert — owner type, then record id; the order the notifications were written plays no part.
        keys.Should().Equal("G-1", "G-2", "W-1");
    }

    [Fact]
    public async Task Handle_ShouldNotSkipATiedEntry_WhenALateNotificationArrivesForIt()
    {
        // Arrange — G-1 and G-2 changed at the same instant; the first page ends on G-1.
        var first = _gadgets.Add("G-1", "First", id: Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var second = _gadgets.Add("G-2", "Second", id: Guid.Parse("00000000-0000-0000-0000-000000000002"));
        Seed(GadgetType, first.Id, hours: 5);
        Seed(GadgetType, second.Id, hours: 5);
        var page1 = await CreateHandler().Handle(new GetMyNotificationsQuery(new CursorRequest(PageSize: 1)), TestContext.Current.CancellationToken);

        // A notification about an older change to G-2 is written late, with the highest id of all.
        Seed(GadgetType, second.Id, hours: 1);

        // Act
        var page2 = await CreateHandler().Handle(new GetMyNotificationsQuery(new CursorRequest(Before: page1.NextCursor, PageSize: 1)), TestContext.Current.CancellationToken);

        // Assert
        page1.Items.Should().ContainSingle().Which.Record.Key.Should().Be("G-1");
        page2.Items.Should().ContainSingle().Which.Record.Key.Should().Be("G-2");
    }

    [Fact]
    public async Task Handle_ShouldNotShiftTheUnreadList_WhenAnEntryIsReadBetweenPages()
    {
        // Arrange — four unread records, newest first G-4..G-1; the first page shows G-4 and G-3.
        var records = Enumerable.Range(1, 4).Select(i => _gadgets.Add($"G-{i}", $"Gadget {i}")).ToList();
        var notifications = records.Select((r, i) => Seed(GadgetType, r.Id, hours: i + 1)).ToList();
        var page1 = await CreateHandler().Handle(new GetMyNotificationsQuery(new CursorRequest(PageSize: 2), UnreadOnly: true), TestContext.Current.CancellationToken);

        // G-4, already shown, is read before the next page is asked for.
        _dbContext.RemoveNotification(notifications[3]);
        Seed(GadgetType, records[3].Id, hours: 4, read: true);

        // Act
        var page2 = await CreateHandler().Handle(new GetMyNotificationsQuery(new CursorRequest(Before: page1.NextCursor, PageSize: 2), UnreadOnly: true), TestContext.Current.CancellationToken);

        // Assert — an offset would now start at G-2's neighbour and skip G-2.
        page1.Items.Select(i => i.Record.Key).Should().Equal("G-4", "G-3");
        page2.Items.Select(i => i.Record.Key).Should().Equal("G-2", "G-1");
        page2.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldMovePastAnEntryDroppedWhileThePageWasBuilt()
    {
        // Arrange — the page's last entry, G-2, is deleted after the page is chosen but before it is named.
        var records = Enumerable.Range(1, 3).Select(i => _gadgets.Add($"G-{i}", $"Gadget {i}")).ToList();
        for (var i = 0; i < records.Count; i++)
        {
            Seed(GadgetType, records[i].Id, hours: i + 1);
        }

        _gadgets.DeleteBeforeDescribe(records[1].Id);

        // Act
        var page1 = await CreateHandler().Handle(new GetMyNotificationsQuery(new CursorRequest(PageSize: 2)), TestContext.Current.CancellationToken);
        var page2 = await CreateHandler().Handle(new GetMyNotificationsQuery(new CursorRequest(Before: page1.NextCursor, PageSize: 2)), TestContext.Current.CancellationToken);

        // Assert — the cursor is G-2's, so the next page starts at G-1 rather than showing G-2's neighbour again.
        page1.Items.Select(i => i.Record.Key).Should().Equal("G-3");
        page2.Items.Select(i => i.Record.Key).Should().Equal("G-1");
    }

    [Fact]
    public async Task Handle_ShouldShowTheLatestChange_EvenWhenAnOlderOneWasWrittenLater()
    {
        // Arrange — durable delivery is out of order, so a later id can describe an earlier change.
        var sprocket = _gadgets.Add("G-1", "Sprocket");
        var newest = Seed(GadgetType, sprocket.Id, hours: 5, NotificationEvent.HealthReported);
        Seed(GadgetType, sprocket.Id, hours: 1, NotificationEvent.TimelineChanged);

        // Act
        var result = await CreateHandler().Handle(new GetMyNotificationsQuery(new CursorRequest()), TestContext.Current.CancellationToken);

        // Assert
        var entry = result.Items.Should().ContainSingle().Subject;
        entry.Event.Should().Be(NotificationEvent.HealthReported);
        entry.LatestNotificationId.Should().Be(newest.Id);
    }

    [Fact]
    public async Task Handle_ShouldReturnAnEmptyPage_ForACallerWithNoEmployee()
    {
        // Arrange
        var sprocket = _gadgets.Add("G-1", "Sprocket");
        Seed(GadgetType, sprocket.Id, hours: 1);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var result = await CreateHandler().Handle(new GetMyNotificationsQuery(new CursorRequest()), TestContext.Current.CancellationToken);

        // Assert
        result.Items.Should().BeEmpty();
        result.NextCursor.Should().BeNull();
    }

    /// <summary>Reads every page by following the cursor, returning the record keys in the order shown.</summary>
    private async Task<List<string>> WalkByCursor(GetMyNotificationsQuery first)
    {
        var keys = new List<string>();
        var query = first;
        for (var pages = 0; pages < 20; pages++)
        {
            var page = await CreateHandler().Handle(query, TestContext.Current.CancellationToken);
            keys.AddRange(page.Items.Select(i => i.Record.Key));
            if (page.NextCursor is null)
            {
                return keys;
            }

            query = query with { Paging = query.Paging with { Before = page.NextCursor } };
        }

        throw new InvalidOperationException("The cursor never reached the last page.");
    }

    private Notification Seed(
        string ownerType,
        Guid ownerId,
        int hours,
        NotificationEvent @event = NotificationEvent.StatusChanged,
        NotificationReason reasons = NotificationReason.Following,
        bool read = false,
        Guid? recipientId = null,
        Guid? actorEmployeeId = null)
    {
        var notification = new NotificationFaker()
            .WithId(_nextId++)
            .WithRecipientEmployeeId(recipientId ?? _employeeId)
            .WithOwner(ownerType, ownerId)
            .WithEvent(@event)
            .WithActorEmployeeId(actorEmployeeId)
            .WithOccurredOn(Start.Plus(Duration.FromHours(hours)))
            .WithReasons(reasons)
            .WithReadOn(read ? Start.Plus(Duration.FromDays(1)) : null)
            .Generate();

        _dbContext.AddNotification(notification);
        return notification;
    }
}
