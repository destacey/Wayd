using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Common.Models;
using Wayd.Planning.Application.Roadmaps.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.Roadmaps;
using Wayd.Planning.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.Planning.Application.Tests.Sut.Roadmaps.Commands;

public sealed class UpdateRoadmapActivityPlacementCommandHandlerTests : IDisposable
{
    private static readonly EventActor Actor = EventActor.User("user-1");
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 1, 0, 0);
    private static readonly LocalDate Today = new(2026, 1, 1);

    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Guid _currentEmployeeId = Guid.NewGuid();
    private readonly UpdateRoadmapActivityPlacementCommandHandler _handler;

    public UpdateRoadmapActivityPlacementCommandHandlerTests()
    {
        _currentPrincipal.Setup(u => u.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_currentEmployeeId);
        _handler = new UpdateRoadmapActivityPlacementCommandHandler(
            _dbContext,
            _currentPrincipal.Object,
            Mock.Of<ICurrentUser>(u => u.GetUserId() == "user-1"),
            new TestingDateTimeProvider(new FakeClock(Now)),
            NullLogger<UpdateRoadmapActivityPlacementCommandHandler>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    private (Roadmap Roadmap, RoadmapActivity Parent, RoadmapActivity Child, RoadmapActivity Grandchild) RoadmapWithAChain()
    {
        var fake = new RoadmapFaker().Generate();
        var roadmap = Roadmap.Create(fake.Name, fake.Description, fake.DateRange, fake.Visibility, [_currentEmployeeId], Actor, Now).Value;
        var caller = new RoadmapActor(_currentEmployeeId, false);
        var parent = roadmap.CreateActivity(new TestUpsertRoadmapActivity(new LocalDateRange(Today, Today.PlusDays(30))), caller, Actor, Now).Value;
        var child = roadmap.CreateActivity(new TestUpsertRoadmapActivity(new LocalDateRange(Today, Today.PlusDays(20)), parent.Id), caller, Actor, Now).Value;
        var grandchild = roadmap.CreateActivity(new TestUpsertRoadmapActivity(new LocalDateRange(Today, Today.PlusDays(10)), child.Id), caller, Actor, Now).Value;
        roadmap.ClearDomainEvents();
        _dbContext.AddRoadmap(roadmap);
        return (roadmap, parent, child, grandchild);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAMoveUnderItsOwnDescendant_AndSaveNothing()
    {
        // Arrange
        var (roadmap, parent, child, grandchild) = RoadmapWithAChain();

        // Act
        var result = await _handler.Handle(
            new UpdateRoadmapActivityPlacementCommand(roadmap.Id, grandchild.Id, parent.Id, 1), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Unable to move the Roadmap Item under itself or one of its own descendants.");
        parent.ParentId.Should().BeNull();
        grandchild.ParentId.Should().Be(child.Id);
        roadmap.DomainEvents.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldMoveAnActivityUpItsChain()
    {
        // Arrange
        var (roadmap, parent, _, grandchild) = RoadmapWithAChain();

        // Act
        var result = await _handler.Handle(
            new UpdateRoadmapActivityPlacementCommand(roadmap.Id, parent.Id, grandchild.Id, 1), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        grandchild.ParentId.Should().Be(parent.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }
}
