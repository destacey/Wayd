using FluentAssertions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Notifications.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Following;
using Wayd.Common.Domain.Notifications;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.Common.Application.Tests.Sut.Notifications.Commands;

public sealed class MuteRecordCommandHandlerTests
{
    private const string GadgetType = "test.mute-record-command.gadget";
    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Gadget");
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 25, 9, 30);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly FakeRecordReadRule _gadgets = new(GadgetType);
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Now));
    private readonly Guid _employeeId = Guid.NewGuid();

    public MuteRecordCommandHandlerTests()
    {
        FollowOwners.Register(Gadget);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
    }

    private MuteRecordCommandHandler CreateHandler() => new(
        [_gadgets],
        new FakeMuteStore(_dbContext),
        new FakeNotificationStore(_dbContext),
        _currentPrincipal.Object,
        _dateTimeProvider);

    [Fact]
    public async Task Handle_ShouldMuteARecordTheCallerCanRead()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket");

        // Act
        var result = await CreateHandler().Handle(new MuteRecordCommand(GadgetType, gadget.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().Be(MuteRecordOutcome.Muted);
        var mute = _dbContext.RecordMutes.Should().ContainSingle().Subject;
        mute.OwnerType.Should().Be(GadgetType);
        mute.OwnerId.Should().Be(gadget.Id);
        mute.EmployeeId.Should().Be(_employeeId);
        mute.MutedOn.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_ShouldMarkTheCallersUnreadNotificationsAboutTheRecordRead_AndNoOthers()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket");
        var mine = new NotificationFaker().WithRecipientEmployeeId(_employeeId).WithOwner(GadgetType, gadget.Id).Generate();
        var mineElsewhere = new NotificationFaker().WithRecipientEmployeeId(_employeeId).WithOwner(GadgetType, Guid.NewGuid()).Generate();
        var someoneElses = new NotificationFaker().WithOwner(GadgetType, gadget.Id).Generate();
        _dbContext.AddNotification(mine);
        _dbContext.AddNotification(mineElsewhere);
        _dbContext.AddNotification(someoneElses);

        // Act
        await CreateHandler().Handle(new MuteRecordCommand(GadgetType, gadget.Id), TestContext.Current.CancellationToken);

        // Assert
        mine.ReadOn.Should().Be(Now);
        mineElsewhere.ReadOn.Should().BeNull();
        someoneElses.ReadOn.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldLeaveTheCallersFollowInPlace()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket");
        _dbContext.AddFollow(Follow.Create(GadgetType, gadget.Id, _employeeId, Now).Value);

        // Act
        await CreateHandler().Handle(new MuteRecordCommand(GadgetType, gadget.Id), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.Follows.Should().ContainSingle()
            .Which.EmployeeId.Should().Be(_employeeId);
        _dbContext.RecordMutes.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldKeepTheFirstMute_WhenTheRecordIsAlreadyMuted()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket");
        await CreateHandler().Handle(new MuteRecordCommand(GadgetType, gadget.Id), TestContext.Current.CancellationToken);
        _dateTimeProvider.Advance(Duration.FromDays(1));

        // Act
        var result = await CreateHandler().Handle(new MuteRecordCommand(GadgetType, gadget.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().Be(MuteRecordOutcome.AlreadyMuted);
        _dbContext.RecordMutes.Should().ContainSingle()
            .Which.MutedOn.Should().Be(Now, "muting again must not reset when the mute began");
    }

    [Fact]
    public async Task Handle_ShouldNotMute_WhenTheCallerCannotReadTheType()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket");
        _gadgets.CanView = false;
        var mine = new NotificationFaker().WithRecipientEmployeeId(_employeeId).WithOwner(GadgetType, gadget.Id).Generate();
        _dbContext.AddNotification(mine);

        // Act
        var result = await CreateHandler().Handle(new MuteRecordCommand(GadgetType, gadget.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().Be(MuteRecordOutcome.NotFound);
        _dbContext.RecordMutes.Should().BeEmpty();
        mine.ReadOn.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldNotMute_WhenTheCallerCannotReadThatRecord()
    {
        // Arrange
        var hidden = _gadgets.Add("G-1", "Sprocket");
        _gadgets.Add("G-2", "Flange");
        _gadgets.Hide(hidden.Id);

        // Act
        var result = await CreateHandler().Handle(new MuteRecordCommand(GadgetType, hidden.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().Be(MuteRecordOutcome.NotFound);
        _dbContext.RecordMutes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldNotMute_WhenTheRecordDoesNotExist()
    {
        // Arrange
        _gadgets.Add("G-1", "Sprocket");

        // Act
        var result = await CreateHandler().Handle(new MuteRecordCommand(GadgetType, Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().Be(MuteRecordOutcome.NotFound);
        _dbContext.RecordMutes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldFail_ForAnOwnerTypeThatCannotBeFollowed()
    {
        // Act
        var result = await CreateHandler().Handle(new MuteRecordCommand("test.mute-record-command.unknown", Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("is not a kind of record that can be followed");
        _dbContext.RecordMutes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheCallerHasNoEmployee()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket");
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var act = () => CreateHandler().Handle(new MuteRecordCommand(GadgetType, gadget.Id), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        _dbContext.RecordMutes.Should().BeEmpty();
    }
}
