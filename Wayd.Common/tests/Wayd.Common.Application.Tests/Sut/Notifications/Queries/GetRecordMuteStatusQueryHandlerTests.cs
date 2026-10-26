using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Notifications.Queries;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Following;
using Wayd.Common.Domain.Notifications;

namespace Wayd.Common.Application.Tests.Sut.Notifications.Queries;

public sealed class GetRecordMuteStatusQueryHandlerTests
{
    private const string GadgetType = "test.record-mute-status.gadget";
    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Gadget");
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 25, 9, 30);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly FakeRecordReadRule _gadgets = new(GadgetType);
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Guid _employeeId = Guid.NewGuid();

    public GetRecordMuteStatusQueryHandlerTests()
    {
        FollowOwners.Register(Gadget);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
    }

    private GetRecordMuteStatusQueryHandler CreateHandler() => new(_dbContext, [_gadgets], _currentPrincipal.Object);

    [Fact]
    public async Task Handle_ShouldSayWhenTheCallerMutedTheRecord()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket");
        _dbContext.AddRecordMute(RecordMute.Create(GadgetType, gadget.Id, _employeeId, Now).Value);

        // Act
        var result = await CreateHandler().Handle(new GetRecordMuteStatusQuery(GadgetType, gadget.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().NotBeNull();
        result.Value!.IsMuted.Should().BeTrue();
        result.Value.MutedOn.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_ShouldSayNotMuted_WhenOnlySomeoneElseMutedTheRecord()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket");
        _dbContext.AddRecordMute(RecordMute.Create(GadgetType, gadget.Id, Guid.NewGuid(), Now).Value);

        // Act
        var result = await CreateHandler().Handle(new GetRecordMuteStatusQuery(GadgetType, gadget.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Value!.IsMuted.Should().BeFalse();
        result.Value.MutedOn.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenTheCallerCannotReadTheRecord_EvenIfTheyMutedIt()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket");
        _dbContext.AddRecordMute(RecordMute.Create(GadgetType, gadget.Id, _employeeId, Now).Value);
        _gadgets.Hide(gadget.Id);

        // Act
        var result = await CreateHandler().Handle(new GetRecordMuteStatusQuery(GadgetType, gadget.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldSayNotMuted_WhenTheCallerHasNoEmployee()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket");
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var result = await CreateHandler().Handle(new GetRecordMuteStatusQuery(GadgetType, gadget.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Value!.IsMuted.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldFail_ForAnOwnerTypeThatCannotBeFollowed()
    {
        // Act
        var result = await CreateHandler().Handle(new GetRecordMuteStatusQuery("test.record-mute-status.unknown", Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}
