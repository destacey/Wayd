using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Interfaces;
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
        Seed(GadgetType, sprocket.Id, hours: 1, NotificationEvent.StatusChanged, NotificationReason.Participant, read: true);
        Seed(GadgetType, sprocket.Id, hours: 5, NotificationEvent.HealthReported, NotificationReason.Following);
        Seed(WidgetType, lever.Id, hours: 3, NotificationEvent.TimelineChanged, NotificationReason.Following);

        // Act
        var result = await CreateHandler().Handle(new GetMyNotificationsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.TotalCount.Should().Be(2);
        result.Items.Select(i => i.Record.Key).Should().Equal("G-1", "W-1");

        var entry = result.Items[0];
        entry.Record.OwnerType.Should().Be(GadgetType);
        entry.Record.Id.Should().Be(sprocket.Id);
        entry.Record.Name.Should().Be("Sprocket");
        entry.Event.Should().Be(NotificationEvent.HealthReported);
        entry.OccurredOn.Should().Be(Start.Plus(Duration.FromHours(5)));
        entry.Count.Should().Be(2);
        entry.UnreadCount.Should().Be(1);
        entry.IsUnread.Should().BeTrue();
        entry.Reasons.Should().BeEquivalentTo([NotificationReason.Following, NotificationReason.Participant]);
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
        var result = await CreateHandler().Handle(new GetMyNotificationsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.TotalCount.Should().Be(1);
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
        var result = await CreateHandler().Handle(new GetMyNotificationsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldListOnlyTheCallersNotifications()
    {
        // Arrange
        var sprocket = _gadgets.Add("G-1", "Sprocket");
        Seed(GadgetType, sprocket.Id, hours: 1, recipientId: Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(new GetMyNotificationsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.TotalCount.Should().Be(0);
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
        var result = await CreateHandler().Handle(new GetMyNotificationsQuery(UnreadOnly: true), TestContext.Current.CancellationToken);

        // Assert
        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Record.Key.Should().Be("G-2");
    }

    [Fact]
    public async Task Handle_ShouldPageTheEntries()
    {
        // Arrange
        for (var i = 1; i <= 3; i++)
        {
            var record = _gadgets.Add($"G-{i}", $"Gadget {i}");
            Seed(GadgetType, record.Id, hours: i);
        }

        // Act
        var page = await CreateHandler().Handle(new GetMyNotificationsQuery(PageNumber: 2, PageSize: 2), TestContext.Current.CancellationToken);

        // Assert
        page.TotalCount.Should().Be(3);
        page.Items.Should().ContainSingle().Which.Record.Key.Should().Be("G-1");
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
        var result = await CreateHandler().Handle(new GetMyNotificationsQuery(), TestContext.Current.CancellationToken);

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
        Seed(GadgetType, sharedId, hours: 1, NotificationEvent.HealthReported, NotificationReason.Participant);
        Seed(WidgetType, sharedId, hours: 2, NotificationEvent.TimelineChanged, NotificationReason.Following);

        // Act
        var result = await CreateHandler().Handle(new GetMyNotificationsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.TotalCount.Should().Be(2);
        result.Items.Select(i => (i.Record.Key, i.Event, i.Count)).Should().Equal(
            ("W-1", NotificationEvent.TimelineChanged, 1),
            ("G-1", NotificationEvent.HealthReported, 1));
        result.Items[1].Reasons.Should().Equal(NotificationReason.Participant);
    }

    [Fact]
    public async Task Handle_ShouldOrderEntriesChangedAtTheSameInstantByTheirNewestNotification()
    {
        // Arrange
        var first = _gadgets.Add("G-1", "First written");
        var second = _gadgets.Add("G-2", "Second written");
        Seed(GadgetType, first.Id, hours: 1);
        Seed(GadgetType, second.Id, hours: 1);

        // Act
        var page1 = await CreateHandler().Handle(new GetMyNotificationsQuery(PageNumber: 1, PageSize: 1), TestContext.Current.CancellationToken);
        var page2 = await CreateHandler().Handle(new GetMyNotificationsQuery(PageNumber: 2, PageSize: 1), TestContext.Current.CancellationToken);

        // Assert
        page1.Items.Should().ContainSingle().Which.Record.Key.Should().Be("G-2");
        page2.Items.Should().ContainSingle().Which.Record.Key.Should().Be("G-1");
    }

    [Fact]
    public async Task Handle_ShouldShowTheLatestChange_EvenWhenAnOlderOneWasWrittenLater()
    {
        // Arrange — durable delivery is out of order, so a later id can describe an earlier change.
        var sprocket = _gadgets.Add("G-1", "Sprocket");
        var newest = Seed(GadgetType, sprocket.Id, hours: 5, NotificationEvent.HealthReported);
        Seed(GadgetType, sprocket.Id, hours: 1, NotificationEvent.TimelineChanged);

        // Act
        var result = await CreateHandler().Handle(new GetMyNotificationsQuery(), TestContext.Current.CancellationToken);

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
        var result = await CreateHandler().Handle(new GetMyNotificationsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
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
