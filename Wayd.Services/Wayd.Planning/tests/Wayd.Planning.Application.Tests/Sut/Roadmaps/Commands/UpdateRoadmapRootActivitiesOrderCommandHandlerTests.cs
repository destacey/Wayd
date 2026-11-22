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

public sealed class UpdateRoadmapRootActivitiesOrderCommandHandlerTests : IDisposable
{
    private static readonly EventActor Actor = EventActor.User("user-1");
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 1, 0, 0);
    private static readonly LocalDate Today = new(2026, 1, 1);

    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Guid _currentEmployeeId = Guid.NewGuid();

    public UpdateRoadmapRootActivitiesOrderCommandHandlerTests()
    {
        _currentPrincipal.Setup(u => u.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_currentEmployeeId);
    }

    public void Dispose() => _dbContext.Dispose();

    private UpdateRoadmapRootActivitiesOrderCommandHandler CreateHandler() =>
        new(_dbContext, _currentPrincipal.Object, Mock.Of<ICurrentUser>(u => u.GetUserId() == "user-1"), new TestingDateTimeProvider(new FakeClock(Now)), NullLogger<UpdateRoadmapRootActivitiesOrderCommandHandler>.Instance);

    private (Roadmap Roadmap, RoadmapActivity First, RoadmapActivity Second) RoadmapWithTwoActivities()
    {
        var fake = new RoadmapFaker().Generate();
        var roadmap = Roadmap.Create(fake.Name, fake.Description, fake.DateRange, fake.Visibility, [_currentEmployeeId], Actor, Now).Value;
        var caller = new RoadmapActor(_currentEmployeeId, false);
        var first = roadmap.CreateActivity(new TestUpsertRoadmapActivity(new LocalDateRange(Today, Today.PlusDays(5))), caller, Actor, Now).Value;
        var second = roadmap.CreateActivity(new TestUpsertRoadmapActivity(new LocalDateRange(Today, Today.PlusDays(5))), caller, Actor, Now).Value;
        roadmap.ClearDomainEvents();
        _dbContext.AddRoadmap(roadmap);
        return (roadmap, first, second);
    }

    [Fact]
    public async Task Handle_ShouldSwapTheOrder()
    {
        // Arrange
        var (_, first, second) = RoadmapWithTwoActivities();
        var command = new UpdateRoadmapRootActivitiesOrderCommand(first.RoadmapId, new() { [first.Id] = 2, [second.Id] = 1 });

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        (first.Order, second.Order).Should().Be((2, 1));
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAnOrderBelowOne_AndChangeNothing()
    {
        // Arrange
        var (roadmap, first, second) = RoadmapWithTwoActivities();
        var orders = (first.Order, second.Order);
        var command = new UpdateRoadmapRootActivitiesOrderCommand(first.RoadmapId, new() { [first.Id] = 2, [second.Id] = 0 });

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Order must be greater than 0.");
        (first.Order, second.Order).Should().Be(orders);
        roadmap.DomainEvents.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
