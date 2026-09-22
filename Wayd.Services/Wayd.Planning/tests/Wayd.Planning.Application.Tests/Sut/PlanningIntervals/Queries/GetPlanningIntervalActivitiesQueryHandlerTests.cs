using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Events;
using Wayd.Planning.Application.PlanningIntervals.Queries;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals.Queries;

public sealed class GetPlanningIntervalActivitiesQueryHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<IActivityLogReader> _activityLogReader = new();
    private readonly GetPlanningIntervalActivitiesQueryHandler _handler;

    public GetPlanningIntervalActivitiesQueryHandlerTests()
    {
        _handler = new GetPlanningIntervalActivitiesQueryHandler(_dbContext, _activityLogReader.Object);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_ReturnsNull_WhenPlanningIntervalDoesNotExist()
    {
        // Act
        var result = await _handler.Handle(
            new GetPlanningIntervalActivitiesQuery(new IdOrKey(Guid.NewGuid())),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        _activityLogReader.Verify(r => r.Read(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReadsThePlanningIntervalsHistory_ByKey()
    {
        // Arrange
        var planningInterval = new PlanningIntervalFaker().WithKey(26).Generate();
        _dbContext.AddPlanningInterval(planningInterval);

        var expected = new PagedResponse<ActivityLogDto>(
        [
            new()
            {
                Id = 1,
                EventType = "PlanningIntervalObjectivesLockedEvent",
                DomainArea = "Planning",
                AggregateType = "PlanningInterval",
                AggregateId = planningInterval.Id,
                ActorKind = EventActorKind.User,
                Timestamp = Instant.FromUnixTimeSeconds(100),
                Payload = "{}",
                Summary = "Planning Interval Objectives Locked"
            }
        ], 1, 1, 50);

        _activityLogReader
            .Setup(r => r.Read(planningInterval.Id, "PlanningInterval", 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _handler.Handle(
            new GetPlanningIntervalActivitiesQuery(new IdOrKey("26"), 1, 50),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expected);
    }
}
