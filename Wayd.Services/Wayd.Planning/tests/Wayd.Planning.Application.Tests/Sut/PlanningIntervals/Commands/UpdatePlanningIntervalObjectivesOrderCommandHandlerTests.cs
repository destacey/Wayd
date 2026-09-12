using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Planning.Application.PlanningIntervals.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals.Commands;

public sealed class UpdatePlanningIntervalObjectivesOrderCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<ILogger<UpdatePlanningIntervalObjectivesOrderCommandHandler>> _logger = new();
    private readonly UpdatePlanningIntervalObjectivesOrderCommandHandler _handler;

    private readonly PlanningInterval _interval;

    public UpdatePlanningIntervalObjectivesOrderCommandHandlerTests()
    {
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        _interval = new PlanningIntervalFaker().WithObjectives(team, 3).Generate();
        _dbContext.AddPlanningInterval(_interval);

        _handler = new UpdatePlanningIntervalObjectivesOrderCommandHandler(_dbContext, _logger.Object);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_WhenEveryIdIsOnTheInterval_ReordersAndSaves()
    {
        // Arrange
        var ids = _interval.Objectives.Select(o => o.Id).ToArray();
        var command = new UpdatePlanningIntervalObjectivesOrderCommand(
            _interval.Id, new Dictionary<Guid, int?> { [ids[0]] = 2, [ids[1]] = 1, [ids[2]] = 3 });

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _interval.Objectives.Single(o => o.Id == ids[0]).Order.Should().Be(2);
        _interval.Objectives.Single(o => o.Id == ids[1]).Order.Should().Be(1);
        _interval.Objectives.Single(o => o.Id == ids[2]).Order.Should().Be(3);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenAnIdIsNotOnTheInterval_FailsWithoutSaving()
    {
        // Arrange
        var known = _interval.Objectives.First();
        var command = new UpdatePlanningIntervalObjectivesOrderCommand(
            _interval.Id, new Dictionary<Guid, int?> { [known.Id] = 1, [Guid.NewGuid()] = 2 });

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Not all objectives");
        known.Order.Should().BeNull();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenIntervalNotFound_Fails()
    {
        // Arrange
        var command = new UpdatePlanningIntervalObjectivesOrderCommand(
            Guid.NewGuid(), new Dictionary<Guid, int?> { [Guid.NewGuid()] = 1 });

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
