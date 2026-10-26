using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Notifications.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Following;
using Wayd.Common.Domain.Notifications;

namespace Wayd.Common.Application.Tests.Sut.Notifications.Commands;

public sealed class UnmuteRecordCommandHandlerTests
{
    private const string GadgetType = "test.unmute-record-command.gadget";
    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Gadget");
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 25, 9, 30);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Guid _employeeId = Guid.NewGuid();

    public UnmuteRecordCommandHandlerTests()
    {
        FollowOwners.Register(Gadget);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
    }

    private UnmuteRecordCommandHandler CreateHandler() => new(new FakeMuteStore(_dbContext), _currentPrincipal.Object);

    [Fact]
    public async Task Handle_ShouldRemoveOnlyTheCallersMuteOfThatRecord()
    {
        // Arrange
        // No read rule is involved: the caller may have lost access to the record since muting it.
        var ownerId = Guid.NewGuid();
        var otherPerson = Guid.NewGuid();
        var otherRecord = Guid.NewGuid();
        _dbContext.AddRecordMute(RecordMute.Create(GadgetType, ownerId, _employeeId, Now).Value);
        _dbContext.AddRecordMute(RecordMute.Create(GadgetType, ownerId, otherPerson, Now).Value);
        _dbContext.AddRecordMute(RecordMute.Create(GadgetType, otherRecord, _employeeId, Now).Value);

        // Act
        var result = await CreateHandler().Handle(new UnmuteRecordCommand(GadgetType, ownerId), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.RecordMutes.AsEnumerable().Select(m => (m.OwnerId, m.EmployeeId)).Should().BeEquivalentTo([
            (ownerId, otherPerson),
            (otherRecord, _employeeId),
        ]);
    }

    [Fact]
    public async Task Handle_ShouldLeaveTheCallersFollowInPlace()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        _dbContext.AddFollow(Follow.Create(GadgetType, ownerId, _employeeId, Now).Value);
        _dbContext.AddRecordMute(RecordMute.Create(GadgetType, ownerId, _employeeId, Now).Value);

        // Act
        await CreateHandler().Handle(new UnmuteRecordCommand(GadgetType, ownerId), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.RecordMutes.Should().BeEmpty();
        _dbContext.Follows.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenTheRecordWasNotMuted()
    {
        // Act
        var result = await CreateHandler().Handle(new UnmuteRecordCommand(GadgetType, Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldFail_ForAnOwnerTypeThatCannotBeFollowed()
    {
        // Act
        var result = await CreateHandler().Handle(new UnmuteRecordCommand("test.unmute-record-command.unknown", Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheCallerHasNoEmployee()
    {
        // Arrange
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var act = () => CreateHandler().Handle(new UnmuteRecordCommand(GadgetType, Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
