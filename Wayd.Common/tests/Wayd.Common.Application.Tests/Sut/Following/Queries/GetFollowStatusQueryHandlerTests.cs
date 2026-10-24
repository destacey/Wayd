using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Following.Queries;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Following;

namespace Wayd.Common.Application.Tests.Sut.Following.Queries;

public sealed class GetFollowStatusQueryHandlerTests
{
    private const string GadgetType = "test.follow-status.gadget";
    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Gadget");
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 23, 9, 30);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly FakeRecordReadRule _gadgets = new(GadgetType);
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Guid _employeeId = Guid.NewGuid();

    public GetFollowStatusQueryHandlerTests()
    {
        FollowOwners.Register(Gadget);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
    }

    private GetFollowStatusQueryHandler CreateHandler() => new(_dbContext, [_gadgets], _currentPrincipal.Object);

    private void SeedFollow(Guid ownerId, Guid employeeId) =>
        _dbContext.Follows.Add(Follow.Create(GadgetType, ownerId, employeeId, Now).Value);

    [Fact]
    public async Task Handle_ShouldSayFollowing_WithWhenTheFollowBegan()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket");
        SeedFollow(gadget.Id, _employeeId);

        // Act
        var result = await CreateHandler().Handle(new GetFollowStatusQuery(GadgetType, gadget.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().NotBeNull();
        result.Value!.IsFollowing.Should().BeTrue();
        result.Value.FollowedOn.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_ShouldSayNotFollowing_WhenOnlySomeoneElseFollowsTheRecord()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket");
        SeedFollow(gadget.Id, Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(new GetFollowStatusQuery(GadgetType, gadget.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Value!.IsFollowing.Should().BeFalse();
        result.Value.FollowedOn.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenTheCallerCannotReadTheRecord_EvenThoughTheyFollowIt()
    {
        // Arrange
        // The follow outlived the caller's access. It must not become a way to confirm the record exists.
        var gadget = _gadgets.Add("G-1", "Sprocket");
        SeedFollow(gadget.Id, _employeeId);
        _gadgets.Hide(gadget.Id);

        // Act
        var result = await CreateHandler().Handle(new GetFollowStatusQuery(GadgetType, gadget.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenTheRecordDoesNotExist()
    {
        // Act
        var result = await CreateHandler().Handle(new GetFollowStatusQuery(GadgetType, Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldSayNotFollowing_WhenTheCallerHasNoEmployee()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket");
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var result = await CreateHandler().Handle(new GetFollowStatusQuery(GadgetType, gadget.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value!.IsFollowing.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldFail_ForAnOwnerTypeThatCannotBeFollowed()
    {
        // Act
        var result = await CreateHandler().Handle(new GetFollowStatusQuery("test.follow-status.unknown", Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("is not a kind of record that can be followed");
    }
}
