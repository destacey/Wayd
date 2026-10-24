using FluentAssertions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Following.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Following;
using Wayd.Tests.Shared;

namespace Wayd.Common.Application.Tests.Sut.Following.Commands;

public sealed class FollowRecordCommandHandlerTests
{
    private const string GadgetType = "test.follow-command.gadget";
    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Gadget");
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 23, 9, 30);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly FakeRecordReadRule _gadgets = new(GadgetType);
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Now));
    private readonly Guid _employeeId = Guid.NewGuid();

    public FollowRecordCommandHandlerTests()
    {
        FollowOwners.Register(Gadget);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
    }

    private FollowRecordCommandHandler CreateHandler() =>
        new([_gadgets], new FakeFollowStore(_dbContext), _currentPrincipal.Object, _dateTimeProvider);

    [Fact]
    public async Task Handle_ShouldFollowARecordTheCallerCanRead()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket");

        // Act
        var result = await CreateHandler().Handle(new FollowRecordCommand(GadgetType, gadget.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().Be(FollowRecordOutcome.Followed);
        var follow = _dbContext.Follows.Should().ContainSingle().Subject;
        follow.OwnerType.Should().Be(GadgetType);
        follow.OwnerId.Should().Be(gadget.Id);
        follow.EmployeeId.Should().Be(_employeeId);
        follow.FollowedOn.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_ShouldBeANoOp_WhenTheCallerAlreadyFollowsTheRecord()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket");
        await CreateHandler().Handle(new FollowRecordCommand(GadgetType, gadget.Id), TestContext.Current.CancellationToken);
        _dateTimeProvider.Advance(Duration.FromDays(1));

        // Act
        var result = await CreateHandler().Handle(new FollowRecordCommand(GadgetType, gadget.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().Be(FollowRecordOutcome.AlreadyFollowing);
        _dbContext.Follows.Should().ContainSingle()
            .Which.FollowedOn.Should().Be(Now, "following again must not reset when the follow began");
    }

    [Fact]
    public async Task Handle_ShouldNotFollow_WhenTheCallerCannotReadTheType()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket");
        _gadgets.CanView = false;

        // Act
        var result = await CreateHandler().Handle(new FollowRecordCommand(GadgetType, gadget.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().Be(FollowRecordOutcome.NotFound);
        _dbContext.Follows.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldNotFollow_WhenTheCallerCannotReadThatRecord()
    {
        // Arrange
        var hidden = _gadgets.Add("G-1", "Sprocket");
        _gadgets.Add("G-2", "Flange");
        _gadgets.Hide(hidden.Id);

        // Act
        var result = await CreateHandler().Handle(new FollowRecordCommand(GadgetType, hidden.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().Be(FollowRecordOutcome.NotFound);
        _dbContext.Follows.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldNotFollow_WhenTheRecordDoesNotExist()
    {
        // Arrange
        _gadgets.Add("G-1", "Sprocket");

        // Act
        var result = await CreateHandler().Handle(new FollowRecordCommand(GadgetType, Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().Be(FollowRecordOutcome.NotFound);
        _dbContext.Follows.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldFail_ForAnOwnerTypeThatCannotBeFollowed()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket");

        // Act
        var result = await CreateHandler().Handle(new FollowRecordCommand("test.follow-command.unknown", gadget.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("is not a kind of record that can be followed");
        _dbContext.Follows.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldFail_ForARegisteredOwnerTypeNoModuleAnswersFor()
    {
        // Arrange
        // Registered but with no read rule, so nothing can say who may read it: refusing is the only
        // safe answer.
        FollowOwners.Register(new FollowOwnerDescriptor("test.follow-command.orphan", "Orphan"));

        // Act
        var result = await CreateHandler().Handle(new FollowRecordCommand("test.follow-command.orphan", Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.Follows.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheCallerHasNoEmployee()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket");
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var act = () => CreateHandler().Handle(new FollowRecordCommand(GadgetType, gadget.Id), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        _dbContext.Follows.Should().BeEmpty();
    }
}
