using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Application.Notifications;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.Following;
using Wayd.Common.Domain.Notifications;
using Wayd.Common.Domain.Tests.Data;

namespace Wayd.Common.Application.Tests.Sut.Notifications;

public sealed class NotificationWriterTests
{
    private const string GadgetType = "test.notification-writer.gadget";
    private const string WidgetType = "test.notification-writer.widget";
    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Gadget");
    private static readonly FollowOwnerDescriptor Widget = new(WidgetType, "Widget");
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 24, 14, 0);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly Mock<IUserService> _userService = new();
    private readonly Mock<IFeatureFlags> _featureFlags = new();
    private readonly Guid _gadgetId = Guid.NewGuid();

    public NotificationWriterTests()
    {
        FollowOwners.Register(Gadget, Widget);
        _featureFlags.Setup(f => f.IsEnabled(FeatureFlags.Names.Following, It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private NotificationWriter CreateWriter() => new(
        _dbContext,
        _dbContext,
        new FakeNotificationStore(_dbContext),
        _userService.Object,
        _featureFlags.Object,
        NullLogger<NotificationWriter>.Instance);

    [Fact]
    public async Task Notify_ShouldTellFollowers_BecauseTheyFollowTheRecord()
    {
        // Arrange
        var follower = AddEmployee();
        Follow(follower);

        // Act
        var added = await CreateWriter().Notify(Change(), [], TestContext.Current.CancellationToken);

        // Assert
        added.Should().Be(1);
        var notification = _dbContext.Notifications.Should().ContainSingle().Subject;
        notification.RecipientEmployeeId.Should().Be(follower);
        notification.Reasons.Should().Be(NotificationReason.Following);
    }

    [Fact]
    public async Task Notify_ShouldTellEachInvolvedPerson_ForTheReasonTheModuleGives()
    {
        // Arrange
        var roleHolder = AddEmployee();

        // Act
        await CreateWriter().Notify(Change(), Role(roleHolder), TestContext.Current.CancellationToken);

        // Assert
        var notification = _dbContext.Notifications.Should().ContainSingle().Subject;
        notification.RecipientEmployeeId.Should().Be(roleHolder);
        notification.Reasons.Should().Be(NotificationReason.Role);
    }

    [Fact]
    public async Task Notify_ShouldWriteOneNotificationWithEveryReason_ForSomeoneWhoQualifiesTwice()
    {
        // Arrange
        var person = AddEmployee();
        Follow(person);

        // Act
        await CreateWriter().Notify(Change(), Role(person, person), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.Notifications.Should().ContainSingle()
            .Which.Reasons.Should().Be(NotificationReason.Following | NotificationReason.Role);
    }

    [Fact]
    public async Task Notify_ShouldMergeTheReasonsOfSomeoneTheModuleNamesTwice()
    {
        // Arrange
        var person = AddEmployee();
        var other = AddEmployee();
        NotificationRecipient[] involved =
        [
            new(person, NotificationReason.Role),
            new(other, NotificationReason.Role),
            new(person, NotificationReason.Following),
        ];

        // Act
        var added = await CreateWriter().Notify(Change(), involved, TestContext.Current.CancellationToken);

        // Assert
        added.Should().Be(2);
        _dbContext.Notifications.Single(n => n.RecipientEmployeeId == person)
            .Reasons.Should().Be(NotificationReason.Following | NotificationReason.Role);
        _dbContext.Notifications.Single(n => n.RecipientEmployeeId == other)
            .Reasons.Should().Be(NotificationReason.Role);
    }

    [Theory]
    [InlineData(NotificationReason.Desk)]
    [InlineData(NotificationReason.Mentioned)]
    [InlineData(NotificationReason.Role | NotificationReason.Desk)]
    [InlineData(NotificationReason.Thread)]
    [InlineData((NotificationReason)0)]
    [InlineData((NotificationReason)256)]
    public async Task Notify_ShouldThrow_ForAReasonNothingProducesYet(NotificationReason reason)
    {
        // Arrange
        var person = AddEmployee();

        // Act
        var act = () => CreateWriter().Notify(Change(), [new(person, reason)], TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
        _dbContext.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task Notify_ShouldRecordTheFactsOfTheChange()
    {
        // Arrange
        var follower = AddEmployee();
        Follow(follower);
        var actorEmployeeId = AddEmployee();
        var change = Change(EventActor.User("actor-user", actorEmployeeId), NotificationEvent.HealthReported);

        // Act
        await CreateWriter().Notify(change, [], TestContext.Current.CancellationToken);

        // Assert
        var notification = _dbContext.Notifications.Should().ContainSingle().Subject;
        notification.OwnerType.Should().Be(GadgetType);
        notification.OwnerId.Should().Be(_gadgetId);
        notification.EventId.Should().Be(change.Source.EventId);
        notification.Event.Should().Be(NotificationEvent.HealthReported);
        notification.ActorKind.Should().Be(EventActorKind.User);
        notification.ActorEmployeeId.Should().Be(actorEmployeeId);
        notification.OccurredOn.Should().Be(Now);
        notification.ReadOn.Should().BeNull();
    }

    [Fact]
    public async Task Notify_ShouldNotTellTheActor_AboutTheirOwnChange()
    {
        // Arrange
        var actor = AddEmployee();
        var other = AddEmployee();
        Follow(actor);

        // Act
        await CreateWriter().Notify(Change(EventActor.User("actor-user", actor)), Role(actor, other), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.Notifications.Should().ContainSingle()
            .Which.RecipientEmployeeId.Should().Be(other);
    }

    [Fact]
    public async Task Notify_ShouldFindTheActorThroughTheirAccount_WhenTheEventCarriesNoEmployee()
    {
        // Arrange
        var actor = AddEmployee();
        Follow(actor);
        _userService.Setup(u => u.GetEmployeeIdAsync("actor-user", It.IsAny<CancellationToken>())).ReturnsAsync(actor);

        // Act
        var added = await CreateWriter().Notify(Change(EventActor.User("actor-user")), [], TestContext.Current.CancellationToken);

        // Assert
        added.Should().Be(0);
        _dbContext.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task Notify_ShouldSkipInactiveAndDeletedEmployees()
    {
        // Arrange
        var current = AddEmployee();
        var inactive = AddEmployee(e => e.AsInactive());
        var deleted = AddEmployee();
        _dbContext.Employees.Single(e => e.Id == deleted).IsDeleted = true;
        Follow(inactive);
        Follow(deleted);

        // Act
        await CreateWriter().Notify(Change(), Role(current, inactive, deleted), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.Notifications.Should().ContainSingle()
            .Which.RecipientEmployeeId.Should().Be(current);
    }

    [Fact]
    public async Task Notify_ShouldIgnoreFollowsOfAnotherOwnerTypeWithTheSameId()
    {
        // Arrange
        var widgetFollower = AddEmployee();
        _dbContext.AddFollow(Domain.Following.Follow.Create(WidgetType, _gadgetId, widgetFollower, Now).Value);

        // Act
        var added = await CreateWriter().Notify(Change(), [], TestContext.Current.CancellationToken);

        // Assert
        added.Should().Be(0);
    }

    [Theory]
    [InlineData(EventActorKind.System)]
    [InlineData(EventActorKind.Import)]
    [InlineData(EventActorKind.Sync)]
    [InlineData(EventActorKind.Anonymous)]
    public async Task Notify_ShouldTellNobody_OfAChangeNoPersonMadeDirectly(EventActorKind kind)
    {
        // Arrange
        var follower = AddEmployee();
        Follow(follower);

        // Act
        var added = await CreateWriter().Notify(Change(new EventActor(kind, "someone")), Role(follower), TestContext.Current.CancellationToken);

        // Assert
        added.Should().Be(0);
        _dbContext.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task Notify_ShouldTellPeople_OfAnAutomationAPersonSetOff()
    {
        // Arrange
        var follower = AddEmployee();
        Follow(follower);

        // Act
        var added = await CreateWriter().Notify(Change(EventActor.Automation("someone", Guid.NewGuid())), [], TestContext.Current.CancellationToken);

        // Assert
        added.Should().Be(1);
        _dbContext.Notifications.Single().ActorKind.Should().Be(EventActorKind.Automation);
    }

    [Fact]
    public async Task Notify_ShouldNotTellThePersonWhoSetOffAnAutomation()
    {
        // Arrange
        var originator = AddEmployee();
        var other = AddEmployee();
        Follow(originator);
        Follow(other);

        // Act
        await CreateWriter().Notify(Change(EventActor.Automation("someone", originator)), [], TestContext.Current.CancellationToken);

        // Assert
        _dbContext.Notifications.Should().ContainSingle()
            .Which.RecipientEmployeeId.Should().Be(other);
    }

    [Theory]
    [InlineData(EventActorKind.User, true, true)]
    [InlineData(EventActorKind.Automation, true, true)]
    [InlineData(EventActorKind.User, false, false)]
    [InlineData(EventActorKind.Import, true, false)]
    [InlineData(EventActorKind.Sync, true, false)]
    [InlineData(EventActorKind.System, true, false)]
    public async Task ShouldNotify_ShouldAllowOnlyChangesAPersonMade_WhileFollowingIsOn(EventActorKind kind, bool flagOn, bool expected)
    {
        // Arrange
        _featureFlags.Setup(f => f.IsEnabled(FeatureFlags.Names.Following, It.IsAny<CancellationToken>())).ReturnsAsync(flagOn);
        var source = new GadgetChangedEvent(new EventActor(kind, "someone"), Now);

        // Act
        var shouldNotify = await CreateWriter().ShouldNotify(source, TestContext.Current.CancellationToken);

        // Assert
        shouldNotify.Should().Be(expected);
    }

    [Fact]
    public async Task Notify_ShouldTellNobody_WhileFollowingIsOff()
    {
        // Arrange
        var follower = AddEmployee();
        Follow(follower);
        _featureFlags.Setup(f => f.IsEnabled(FeatureFlags.Names.Following, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var added = await CreateWriter().Notify(Change(), Role(follower), TestContext.Current.CancellationToken);

        // Assert
        added.Should().Be(0);
        _dbContext.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task Notify_ShouldWriteNothing_WhenTheSameEventIsDeliveredAgain()
    {
        // Arrange
        var follower = AddEmployee();
        Follow(follower);
        var change = Change();
        await CreateWriter().Notify(change, [], TestContext.Current.CancellationToken);

        // Act
        var added = await CreateWriter().Notify(change, [], TestContext.Current.CancellationToken);

        // Assert
        added.Should().Be(0);
        _dbContext.Notifications.Should().ContainSingle();
    }

    [Fact]
    public async Task Notify_ShouldNotTellSomeoneWhoMutedTheRecord_WhateverTheirReasons()
    {
        // Arrange
        var muter = AddEmployee();
        var other = AddEmployee();
        Follow(muter);
        Follow(other);
        _dbContext.AddRecordMute(RecordMute.Create(GadgetType, _gadgetId, muter, Now).Value);

        // Act
        var added = await CreateWriter().Notify(Change(), Role(muter, other), TestContext.Current.CancellationToken);

        // Assert
        added.Should().Be(1);
        _dbContext.Notifications.Should().ContainSingle()
            .Which.RecipientEmployeeId.Should().Be(other);
    }

    [Fact]
    public async Task Notify_ShouldIgnoreAMuteOfAnotherRecordWithTheSameId()
    {
        // Arrange
        var follower = AddEmployee();
        Follow(follower);
        _dbContext.AddRecordMute(RecordMute.Create(WidgetType, _gadgetId, follower, Now).Value);
        _dbContext.AddRecordMute(RecordMute.Create(GadgetType, Guid.NewGuid(), follower, Now).Value);

        // Act
        var added = await CreateWriter().Notify(Change(), [], TestContext.Current.CancellationToken);

        // Assert
        added.Should().Be(1);
    }

    [Fact]
    public async Task Notify_ShouldStillTellSomeone_ForTheReasonTheyDidNotMute()
    {
        // Arrange
        var person = AddEmployee();
        Follow(person);
        _dbContext.AddReasonMute(ReasonMute.Create(person, NotificationReason.Role, Now).Value);

        // Act
        await CreateWriter().Notify(Change(), Role(person), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.Notifications.Should().ContainSingle()
            .Which.Reasons.Should().Be(NotificationReason.Following);
    }

    [Theory]
    [InlineData(NotificationReason.Following)]
    [InlineData(NotificationReason.Role)]
    public async Task Notify_ShouldNotTellSomeone_WhoMutedTheirOnlyReason(NotificationReason muted)
    {
        // Arrange
        var muter = AddEmployee();
        var other = AddEmployee();
        if (muted == NotificationReason.Following)
        {
            Follow(muter);
            Follow(other);
        }

        _dbContext.AddReasonMute(ReasonMute.Create(muter, muted, Now).Value);
        NotificationRecipient[] involved = muted == NotificationReason.Role ? Role(muter, other) : [];

        // Act
        await CreateWriter().Notify(Change(), involved, TestContext.Current.CancellationToken);

        // Assert
        var notification = _dbContext.Notifications.Should().ContainSingle().Subject;
        notification.RecipientEmployeeId.Should().Be(other);
        notification.Reasons.Should().Be(muted);
    }

    [Fact]
    public async Task Notify_ShouldNotTellSomeone_WhoMutedEveryReasonTheyHave()
    {
        // Arrange
        var person = AddEmployee();
        Follow(person);
        _dbContext.AddReasonMute(ReasonMute.Create(person, NotificationReason.Following, Now).Value);
        _dbContext.AddReasonMute(ReasonMute.Create(person, NotificationReason.Role, Now).Value);

        // Act
        var added = await CreateWriter().Notify(Change(), Role(person), TestContext.Current.CancellationToken);

        // Assert
        added.Should().Be(0);
        _dbContext.Notifications.Should().BeEmpty();
    }

    private Guid AddEmployee(Func<EmployeeFaker, EmployeeFaker>? configure = null)
    {
        var faker = new EmployeeFaker().AsActive();
        var employee = (configure?.Invoke(faker) ?? faker).Generate();
        _dbContext.AddEmployee(employee);
        return employee.Id;
    }

    private static NotificationRecipient[] Role(params Guid[] employeeIds) =>
        [.. NotificationRecipient.For(NotificationReason.Role, employeeIds)];

    private void Follow(Guid employeeId) =>
        _dbContext.AddFollow(Domain.Following.Follow.Create(GadgetType, _gadgetId, employeeId, Now).Value);

    private RecordChange Change(EventActor? actor = null, NotificationEvent @event = NotificationEvent.StatusChanged) =>
        new(GadgetType, _gadgetId, @event, new GadgetChangedEvent(actor ?? EventActor.User("actor-user", Guid.NewGuid()), Now));

    private sealed record GadgetChangedEvent : DomainEvent<GadgetChangedEvent>, IDomainEventDescriptor
    {
        public GadgetChangedEvent(EventActor actor, Instant timestamp)
            : base(actor, "1.0")
        {
            Timestamp = timestamp;
        }

        public static ActivityCategory ActivityCategory => ActivityCategory.Updated;
    }
}
