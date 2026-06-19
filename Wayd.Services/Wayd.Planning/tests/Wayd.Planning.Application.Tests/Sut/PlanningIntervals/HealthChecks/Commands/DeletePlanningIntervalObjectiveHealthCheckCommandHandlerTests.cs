using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Planning.Application.PlanningIntervals.HealthChecks.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals.HealthChecks.Commands;

public class DeletePlanningIntervalObjectiveHealthCheckCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly DeletePlanningIntervalObjectiveHealthCheckCommandHandler _handler;
    private readonly Mock<ILogger<DeletePlanningIntervalObjectiveHealthCheckCommandHandler>> _mockLogger = new();
    private readonly Instant _now = Instant.FromUtc(2026, 4, 1, 0, 0);
    private readonly PlanningIntervalObjectiveFaker _objectiveFaker;

    public DeletePlanningIntervalObjectiveHealthCheckCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();

        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        _objectiveFaker = new PlanningIntervalObjectiveFaker(Guid.NewGuid(), team, ObjectiveStatus.NotStarted, false);

        _handler = new DeletePlanningIntervalObjectiveHealthCheckCommandHandler(_dbContext, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WhenHealthCheckExists_RemovesAndSaves()
    {
        var objective = _objectiveFaker.Generate();
        var hc = objective.AddHealthCheck(HealthStatus.Healthy, Guid.NewGuid(), _now.Plus(Duration.FromDays(7)), null, _now).Value;
        _dbContext.AddPlanningIntervalObjective(objective);

        var command = new DeletePlanningIntervalObjectiveHealthCheckCommand(objective.Id, hc.Id);

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        objective.HealthChecks.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenObjectiveNotFound_ReturnsFailure()
    {
        var command = new DeletePlanningIntervalObjectiveHealthCheckCommand(Guid.NewGuid(), Guid.NewGuid());

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenHealthCheckNotFoundOnObjective_ReturnsFailure()
    {
        var objective = _objectiveFaker.Generate();
        _dbContext.AddPlanningIntervalObjective(objective);
        var unknownHcId = Guid.NewGuid();

        var command = new DeletePlanningIntervalObjectiveHealthCheckCommand(objective.Id, unknownHcId);

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(unknownHcId.ToString());
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
