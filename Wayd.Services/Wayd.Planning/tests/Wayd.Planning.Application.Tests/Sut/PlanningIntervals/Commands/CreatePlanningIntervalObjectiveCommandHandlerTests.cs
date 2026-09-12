using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Planning.Application.PlanningIntervals.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals.Commands;

public sealed class CreatePlanningIntervalObjectiveCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<ILogger<CreatePlanningIntervalObjectiveCommandHandler>> _logger = new();
    private readonly CreatePlanningIntervalObjectiveCommandHandler _handler;

    private readonly PlanningInterval _interval;
    private readonly PlanningTeam _team;

    public CreatePlanningIntervalObjectiveCommandHandlerTests()
    {
        _interval = new PlanningIntervalFaker().Generate();
        _team = new PlanningTeamFaker(TeamType.Team).Generate();

        _dbContext.AddPlanningInterval(_interval);
        _dbContext.AddPlanningTeam(_team);

        _handler = new CreatePlanningIntervalObjectiveCommandHandler(_dbContext, _logger.Object);
    }

    public void Dispose() => _dbContext.Dispose();

    private CreatePlanningIntervalObjectiveCommand Command(Guid? intervalId = null, Guid? teamId = null) =>
        new(intervalId ?? _interval.Id, teamId ?? _team.Id, "Ship the thing", "Because", null, null, true, 4);

    [Fact]
    public async Task Handle_WhenValid_AddsTheObjectiveToTheIntervalAndSaves()
    {
        // Arrange
        var command = Command();

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var objective = _interval.Objectives.Should().ContainSingle().Subject;
        result.Value.Id.Should().Be(objective.Id);
        objective.Name.Should().Be("Ship the thing");
        objective.Description.Should().Be("Because");
        objective.TeamId.Should().Be(_team.Id);
        objective.Type.Should().Be(PlanningIntervalObjectiveType.Team);
        objective.Status.Should().Be(ObjectiveStatus.NotStarted);
        objective.IsStretch.Should().BeTrue();
        objective.Order.Should().Be(4);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenIntervalNotFound_Fails()
    {
        // Arrange
        var command = Command(intervalId: Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTeamNotFound_Fails()
    {
        // Arrange
        var command = Command(teamId: Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Team not found");
        _interval.Objectives.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenObjectivesAreLocked_Fails()
    {
        // Arrange
        var locked = new PlanningIntervalFaker().WithObjectivesLocked(true).Generate();
        _dbContext.AddPlanningInterval(locked);

        // Act
        var result = await _handler.Handle(Command(intervalId: locked.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("locked");
        locked.Objectives.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
