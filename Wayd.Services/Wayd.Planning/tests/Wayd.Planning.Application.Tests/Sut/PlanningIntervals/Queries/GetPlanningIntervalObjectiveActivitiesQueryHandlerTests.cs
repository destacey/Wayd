using FluentAssertions;
using Moq;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Application.PlanningIntervals.Queries;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals.Queries;

public sealed class GetPlanningIntervalObjectiveActivitiesQueryHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<IActivityLogReader> _activityLogReader = new();
    private readonly GetPlanningIntervalObjectiveActivitiesQueryHandler _handler;

    public GetPlanningIntervalObjectiveActivitiesQueryHandlerTests()
    {
        _handler = new GetPlanningIntervalObjectiveActivitiesQueryHandler(_dbContext, _activityLogReader.Object);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_ReturnsNull_WhenTheObjectiveBelongsToAnotherPlanningInterval()
    {
        // Arrange
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        var planningInterval = new PlanningIntervalFaker().Generate();
        var other = new PlanningIntervalFaker().Generate();
        var objective = new PlanningIntervalObjectiveFaker(other.Id, team, ObjectiveStatus.NotStarted, false).Generate();
        _dbContext.AddPlanningIntervals([planningInterval, other]);
        _dbContext.AddPlanningIntervalObjective(objective);

        // Act
        var result = await _handler.Handle(
            new GetPlanningIntervalObjectiveActivitiesQuery(new IdOrKey(planningInterval.Id), new IdOrKey(objective.Id)),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        _activityLogReader.Verify(r => r.Read(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReadsTheObjectivesHistory()
    {
        // Arrange
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        var planningInterval = new PlanningIntervalFaker().Generate();
        var objective = new PlanningIntervalObjectiveFaker(planningInterval.Id, team, ObjectiveStatus.InProgress, false).Generate();
        _dbContext.AddPlanningInterval(planningInterval);
        _dbContext.AddPlanningIntervalObjective(objective);

        var expected = new PagedResponse<ActivityLogDto>([], 0, 2, 25);
        _activityLogReader
            .Setup(r => r.Read(objective.Id, "PlanningIntervalObjective", 2, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _handler.Handle(
            new GetPlanningIntervalObjectiveActivitiesQuery(new IdOrKey(planningInterval.Id), new IdOrKey(objective.Id), 2, 25),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(expected);
    }
}
