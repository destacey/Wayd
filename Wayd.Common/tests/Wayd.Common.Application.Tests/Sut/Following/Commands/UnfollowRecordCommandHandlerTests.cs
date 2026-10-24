using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Following.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Following;

namespace Wayd.Common.Application.Tests.Sut.Following.Commands;

public sealed class UnfollowRecordCommandHandlerTests
{
    private const string GadgetType = "test.unfollow-command.gadget";
    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Gadget");
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 23, 9, 30);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Guid _employeeId = Guid.NewGuid();

    public UnfollowRecordCommandHandlerTests()
    {
        FollowOwners.Register(Gadget);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
    }

    private UnfollowRecordCommandHandler CreateHandler() =>
        new(new FakeFollowStore(_dbContext), _currentPrincipal.Object);

    private Follow SeedFollow(Guid ownerId, Guid employeeId)
    {
        var follow = Follow.Create(GadgetType, ownerId, employeeId, Now).Value;
        _dbContext.Follows.Add(follow);
        return follow;
    }

    [Fact]
    public async Task Handle_ShouldRemoveOnlyTheCallersFollowOfThatRecord()
    {
        // Arrange
        var gadgetId = Guid.NewGuid();
        var otherGadgetId = Guid.NewGuid();
        var someoneElse = Guid.NewGuid();
        SeedFollow(gadgetId, _employeeId);
        var keptOtherRecord = SeedFollow(otherGadgetId, _employeeId);
        var keptOtherPerson = SeedFollow(gadgetId, someoneElse);

        // Act
        var result = await CreateHandler().Handle(new UnfollowRecordCommand(GadgetType, gadgetId), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.Follows.Should().BeEquivalentTo([keptOtherRecord, keptOtherPerson]);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenTheCallerDoesNotFollowTheRecord()
    {
        // Arrange
        var kept = SeedFollow(Guid.NewGuid(), _employeeId);

        // Act
        var result = await CreateHandler().Handle(new UnfollowRecordCommand(GadgetType, Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.Follows.Should().ContainSingle().Which.Should().Be(kept);
    }

    [Fact]
    public async Task Handle_ShouldMatchTheOwnerTypeWithoutCase()
    {
        // Arrange
        var gadgetId = Guid.NewGuid();
        SeedFollow(gadgetId, _employeeId);

        // Act
        var result = await CreateHandler().Handle(new UnfollowRecordCommand(GadgetType.ToUpperInvariant(), gadgetId), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.Follows.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldFail_ForAnOwnerTypeThatCannotBeFollowed()
    {
        // Arrange
        var kept = SeedFollow(Guid.NewGuid(), _employeeId);

        // Act
        var result = await CreateHandler().Handle(new UnfollowRecordCommand("test.unfollow-command.unknown", kept.OwnerId), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("is not a kind of record that can be followed");
        _dbContext.Follows.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheCallerHasNoEmployee()
    {
        // Arrange
        var kept = SeedFollow(Guid.NewGuid(), _employeeId);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var act = () => CreateHandler().Handle(new UnfollowRecordCommand(GadgetType, kept.OwnerId), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        _dbContext.Follows.Should().ContainSingle();
    }
}
