using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Planning.Application.PlanningIntervals.HealthChecks.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals.HealthChecks.Commands;

public class CreatePlanningIntervalObjectiveHealthCheckCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly CreatePlanningIntervalObjectiveHealthCheckCommandHandler _handler;
    private readonly Mock<ILogger<CreatePlanningIntervalObjectiveHealthCheckCommandHandler>> _mockLogger = new();
    private readonly Mock<ICurrentUser> _mockCurrentUser = new();
    private readonly Mock<IDateTimeProvider> _mockDateTimeProvider = new();
    private readonly Guid _currentEmployeeId = Guid.NewGuid();
    private readonly Instant _now = Instant.FromUtc(2026, 4, 1, 0, 0);
    private readonly PlanningIntervalObjectiveFaker _objectiveFaker;

    public CreatePlanningIntervalObjectiveHealthCheckCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockCurrentUser.Setup(u => u.GetEmployeeId()).Returns(_currentEmployeeId);
        _mockDateTimeProvider.Setup(d => d.Now).Returns(_now);

        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        _objectiveFaker = new PlanningIntervalObjectiveFaker(Guid.NewGuid(), team, ObjectiveStatus.NotStarted, false);

        _handler = new CreatePlanningIntervalObjectiveHealthCheckCommandHandler(
            _dbContext, _mockDateTimeProvider.Object, _mockCurrentUser.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WhenValid_AddsHealthCheckAndSaves()
    {
        var objective = _objectiveFaker.Generate();
        _dbContext.AddPlanningIntervalObjective(objective);

        var command = new CreatePlanningIntervalObjectiveHealthCheckCommand(
            objective.Id, HealthStatus.Healthy, _now.Plus(Duration.FromDays(7)), "all good");

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        objective.HealthChecks.Should().ContainSingle();
        objective.HealthChecks.Single().Id.Should().Be(result.Value);
        objective.HealthChecks.Single().ReportedById.Should().Be(_currentEmployeeId);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenObjectiveNotFound_ReturnsFailure()
    {
        var command = new CreatePlanningIntervalObjectiveHealthCheckCommand(
            Guid.NewGuid(), HealthStatus.Healthy, _now.Plus(Duration.FromDays(7)), null);

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserHasNoEmployeeId_ReturnsFailure()
    {
        _mockCurrentUser.Setup(u => u.GetEmployeeId()).Returns((Guid?)null);

        var objective = _objectiveFaker.Generate();
        _dbContext.AddPlanningIntervalObjective(objective);

        var command = new CreatePlanningIntervalObjectiveHealthCheckCommand(
            objective.Id, HealthStatus.Healthy, _now.Plus(Duration.FromDays(7)), null);

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("employee Id");
        objective.HealthChecks.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenAggregateRejectsAdd_ReturnsFailureFromAggregate()
    {
        var objective = _objectiveFaker.Generate();
        _dbContext.AddPlanningIntervalObjective(objective);

        // Expiration in the past — domain method returns failure before persistence.
        var command = new CreatePlanningIntervalObjectiveHealthCheckCommand(
            objective.Id, HealthStatus.Healthy, _now.Minus(Duration.FromMinutes(1)), null);

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Expiration must be in the future.");
        objective.HealthChecks.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
