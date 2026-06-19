using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Planning.Application.PlanningIntervals.HealthChecks.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals.HealthChecks.Commands;

public class UpdatePlanningIntervalObjectiveHealthCheckCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly UpdatePlanningIntervalObjectiveHealthCheckCommandHandler _handler;
    private readonly Mock<ILogger<UpdatePlanningIntervalObjectiveHealthCheckCommandHandler>> _mockLogger = new();
    private readonly Mock<IDateTimeProvider> _mockDateTimeProvider = new();
    private readonly Instant _now = Instant.FromUtc(2026, 4, 1, 0, 0);
    private readonly PlanningIntervalObjectiveFaker _objectiveFaker;
    private readonly EmployeeFaker _employeeFaker = new();

    public UpdatePlanningIntervalObjectiveHealthCheckCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockDateTimeProvider.Setup(d => d.Now).Returns(_now);

        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        _objectiveFaker = new PlanningIntervalObjectiveFaker(Guid.NewGuid(), team, ObjectiveStatus.NotStarted, false);

        _handler = new UpdatePlanningIntervalObjectiveHealthCheckCommandHandler(
            _dbContext, _mockDateTimeProvider.Object, _mockLogger.Object);
    }

    private (PlanningIntervalObjective objective, PlanningIntervalObjectiveHealthCheck healthCheck) Seed()
    {
        var employee = _employeeFaker.Generate();
        var objective = _objectiveFaker.Generate();
        var hc = objective.AddHealthCheck(HealthStatus.Healthy, employee.Id, _now.Plus(Duration.FromDays(7)), "old", _now).Value;

        // Hydrate the ReportedBy navigation that EF would normally populate via Include — needed for
        // the handler's Mapster .Adapt<>() call after the update.
        typeof(Wayd.Common.Domain.Models.HealthChecks.HealthCheckBase)
            .GetProperty(nameof(PlanningIntervalObjectiveHealthCheck.ReportedBy))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(hc, [employee]);

        _dbContext.AddPlanningIntervalObjective(objective);
        return (objective, hc);
    }

    [Fact]
    public async Task Handle_WhenValid_AppliesUpdateToAggregateAndSaves()
    {
        var (objective, hc) = Seed();
        var newExpiration = _now.Plus(Duration.FromDays(14));
        var command = new UpdatePlanningIntervalObjectiveHealthCheckCommand(
            objective.Id, hc.Id, HealthStatus.AtRisk, newExpiration, "new note");

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        // The Mapster-mapped DTO shape is exercised by the global IMapFrom registration in production
        // (AddPlanningApplication) and verified by integration tests. Here we assert that the aggregate
        // mutated and SaveChanges was called — the meaningful unit-level behavior.
        hc.Status.Should().Be(HealthStatus.AtRisk);
        hc.Expiration.Should().Be(newExpiration);
        hc.Note.Should().Be("new note");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenObjectiveNotFound_ReturnsFailure()
    {
        var command = new UpdatePlanningIntervalObjectiveHealthCheckCommand(
            Guid.NewGuid(), Guid.NewGuid(), HealthStatus.AtRisk, _now.Plus(Duration.FromDays(7)), null);

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
