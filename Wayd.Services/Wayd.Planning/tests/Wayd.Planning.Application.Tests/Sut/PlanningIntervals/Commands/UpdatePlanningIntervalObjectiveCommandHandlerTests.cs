using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Planning.Application.PlanningIntervals.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals.Commands;

public sealed class UpdatePlanningIntervalObjectiveCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<ILogger<UpdatePlanningIntervalObjectiveCommandHandler>> _logger = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();
    private readonly Instant _now = Instant.FromUtc(2026, 4, 1, 0, 0);
    private readonly UpdatePlanningIntervalObjectiveCommandHandler _handler;

    private readonly PlanningTeam _team;

    public UpdatePlanningIntervalObjectiveCommandHandlerTests()
    {
        _dateTimeProvider.Setup(d => d.Now).Returns(_now);
        _team = new PlanningTeamFaker(TeamType.Team).Generate();

        _handler = new UpdatePlanningIntervalObjectiveCommandHandler(_dbContext, _dateTimeProvider.Object, _logger.Object);
    }

    public void Dispose() => _dbContext.Dispose();

    private PlanningInterval IntervalWithOneObjective(bool locked = false)
    {
        var interval = new PlanningIntervalFaker().WithObjectives(_team, 1).WithObjectivesLocked(locked).Generate();
        _dbContext.AddPlanningInterval(interval);
        return interval;
    }

    [Fact]
    public async Task Handle_WhenUnlocked_UpdatesTheObjectiveAndSaves()
    {
        // Arrange
        var interval = IntervalWithOneObjective();
        var objective = interval.Objectives.Single();
        var command = new UpdatePlanningIntervalObjectiveCommand(
            interval.Id, objective.Id, "Renamed", "Described", ObjectiveStatus.Completed, 100, interval.DateRange.Start, interval.DateRange.End, true);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(objective.Key);
        objective.Name.Should().Be("Renamed");
        objective.Description.Should().Be("Described");
        objective.Status.Should().Be(ObjectiveStatus.Completed);
        objective.Progress.Should().Be(100);
        objective.StartDate.Should().Be(interval.DateRange.Start);
        objective.TargetDate.Should().Be(interval.DateRange.End);
        objective.IsStretch.Should().BeTrue();
        objective.ClosedDate.Should().Be(_now);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenLocked_KeepsNameAndStretchButRecordsProgress()
    {
        // Arrange
        var interval = IntervalWithOneObjective(locked: true);
        var objective = interval.Objectives.Single();
        var originalName = objective.Name;
        var command = new UpdatePlanningIntervalObjectiveCommand(
            interval.Id, objective.Id, "Renamed", null, ObjectiveStatus.InProgress, 30, null, null, true);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        objective.Name.Should().Be(originalName);
        objective.IsStretch.Should().BeFalse();
        objective.Status.Should().Be(ObjectiveStatus.InProgress);
        objective.Progress.Should().Be(30);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenIntervalNotFound_Fails()
    {
        // Arrange
        var command = new UpdatePlanningIntervalObjectiveCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Renamed", null, ObjectiveStatus.InProgress, 0, null, null, false);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenObjectiveNotOnTheInterval_Fails()
    {
        // Arrange
        var interval = IntervalWithOneObjective();
        var command = new UpdatePlanningIntervalObjectiveCommand(
            interval.Id, Guid.NewGuid(), "Renamed", null, ObjectiveStatus.InProgress, 0, null, null, false);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
