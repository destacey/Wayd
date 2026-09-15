using Microsoft.Extensions.Logging;
using Wayd.Common.Models;
using Wayd.Planning.Application.PlanningIntervals.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Tests.Data;
using Wayd.Tests.Shared.Extensions;
using Moq;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Application.Interfaces;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals.Commands;

public class MapPlanningIntervalSprintsCommandHandlerTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30);

    private readonly FakePlanningDbContext _dbContext;
    private readonly MapPlanningIntervalSprintsCommandHandler _handler;
    private readonly Mock<ILogger<MapPlanningIntervalSprintsCommandHandler>> _mockLogger;

    private readonly PlanningIntervalFaker _planningIntervalFaker;
    private readonly IterationFaker _iterationFaker;

    public MapPlanningIntervalSprintsCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<MapPlanningIntervalSprintsCommandHandler>>();

        _handler = new MapPlanningIntervalSprintsCommandHandler(_dbContext, Mock.Of<ICurrentUser>(), Mock.Of<IDateTimeProvider>(), _mockLogger.Object);

        _planningIntervalFaker = new PlanningIntervalFaker();
        _iterationFaker = new IterationFaker();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenPlanningIntervalDoesNotExist()
    {
        // Arrange
        var command = new MapPlanningIntervalSprintsCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            []);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTeamIsNotPartOfPlanningInterval()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2024, 1, 1), new LocalDate(2024, 3, 31));
        var teamId = Guid.NewGuid();
        var otherTeamId = Guid.NewGuid();

        var planningInterval = _planningIntervalFaker
            .WithDateRange(piDates)
            .WithTeams(teamId)
            .WithIterations(piDates, 2, "Iteration ")
            .Generate();

        _dbContext.AddPlanningInterval(planningInterval);

        var command = new MapPlanningIntervalSprintsCommand(
            planningInterval.Id,
            otherTeamId, // Different team not in PI
            []);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not part of");
    }

    [Fact]
    public async Task Handle_ShouldMapSprints_WhenValidCommand()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2024, 1, 1), new LocalDate(2024, 3, 31));
        var teamId = Guid.NewGuid();

        var planningInterval = _planningIntervalFaker
            .WithDateRange(piDates)
            .WithTeams(teamId)
            .WithIterations(piDates, 2, "Iteration ")
            .Generate();

        var sprint1 = _iterationFaker.AsSprint().WithTeamId(teamId).Generate();
        var sprint2 = _iterationFaker.AsSprint().WithTeamId(teamId).Generate();

        _dbContext.AddPlanningInterval(planningInterval);
        _dbContext.AddIteration(sprint1);
        _dbContext.AddIteration(sprint2);

        var iteration1Id = planningInterval.Iterations.First().Id;
        var iteration2Id = planningInterval.Iterations.Last().Id;

        var command = new MapPlanningIntervalSprintsCommand(
            planningInterval.Id,
            teamId,
            new Dictionary<Guid, Guid?>
            {
                { iteration1Id, sprint1.Id },
                { iteration2Id, sprint2.Id }
            });

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        planningInterval.IterationSprints.Should().HaveCount(2);
        planningInterval.IterationSprints.Should().Contain(s => s.SprintId == sprint1.Id && s.PlanningIntervalIterationId == iteration1Id);
        planningInterval.IterationSprints.Should().Contain(s => s.SprintId == sprint2.Id && s.PlanningIntervalIterationId == iteration2Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldUnmapSprints_WhenNullSprintIdProvided()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2024, 1, 1), new LocalDate(2024, 3, 31));
        var teamId = Guid.NewGuid();

        var planningInterval = _planningIntervalFaker
            .WithDateRange(piDates)
            .WithTeams(teamId)
            .WithIterations(piDates, 2, "Iteration ")
            .Generate();

        var sprint = _iterationFaker.AsSprint().WithTeamId(teamId).Generate();

        _dbContext.AddPlanningInterval(planningInterval);
        _dbContext.AddIteration(sprint);

        var iterationId = planningInterval.Iterations.First().Id;

        // Map sprint first
        planningInterval.MapSprintToIteration(iterationId, sprint, EventActor.System, Now);
        planningInterval.IterationSprints.Should().HaveCount(1);

        // Set up Sprint navigation property for domain logic
        foreach (var mapping in planningInterval.IterationSprints)
        {
            if (mapping.SprintId == sprint.Id)
                mapping.SetPrivate(m => m.Sprint, sprint);
        }

        var command = new MapPlanningIntervalSprintsCommand(
            planningInterval.Id,
            teamId,
            new Dictionary<Guid, Guid?>
            {
                { iterationId, null } // Unmap
            });

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        planningInterval.IterationSprints.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldUnmapOrphanedSprints_WhenSyncingTeamSprints()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2024, 1, 1), new LocalDate(2024, 3, 31));
        var teamId = Guid.NewGuid();

        var planningInterval = _planningIntervalFaker
            .WithDateRange(piDates)
            .WithTeams(teamId)
            .WithIterations(piDates, 2, "Iteration ")
            .Generate();

        var sprint1 = _iterationFaker.AsSprint().WithTeamId(teamId).Generate();
        var sprint2 = _iterationFaker.AsSprint().WithTeamId(teamId).Generate();
        var sprint3 = _iterationFaker.AsSprint().WithTeamId(teamId).Generate();

        _dbContext.AddPlanningInterval(planningInterval);
        _dbContext.AddIteration(sprint1);
        _dbContext.AddIteration(sprint2);
        _dbContext.AddIteration(sprint3);

        var iteration1Id = planningInterval.Iterations.First().Id;
        var iteration2Id = planningInterval.Iterations.Last().Id;

        // Map sprint1 and sprint2 initially
        planningInterval.MapSprintToIteration(iteration1Id, sprint1, EventActor.System, Now);
        planningInterval.MapSprintToIteration(iteration2Id, sprint2, EventActor.System, Now);
        planningInterval.IterationSprints.Should().HaveCount(2);

        // Set up Sprint navigation properties for domain logic
        foreach (var mapping in planningInterval.IterationSprints)
        {
            if (mapping.SprintId == sprint1.Id)
                mapping.SetPrivate(m => m.Sprint, sprint1);
            if (mapping.SprintId == sprint2.Id)
                mapping.SetPrivate(m => m.Sprint, sprint2);
        }

        // Command only includes sprint3 - sprint1 and sprint2 should be unmapped
        var command = new MapPlanningIntervalSprintsCommand(
            planningInterval.Id,
            teamId,
            new Dictionary<Guid, Guid?>
            {
                { iteration1Id, sprint3.Id }
            });

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        planningInterval.IterationSprints.Should().HaveCount(1);
        planningInterval.IterationSprints.First().SprintId.Should().Be(sprint3.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenSprintDoesNotExist()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2024, 1, 1), new LocalDate(2024, 3, 31));
        var teamId = Guid.NewGuid();

        var planningInterval = _planningIntervalFaker
            .WithDateRange(piDates)
            .WithTeams(teamId)
            .WithIterations(piDates, 2, "Iteration ")
            .Generate();

        _dbContext.AddPlanningInterval(planningInterval);

        var iterationId = planningInterval.Iterations.First().Id;
        var nonExistentSprintId = Guid.NewGuid();

        var command = new MapPlanningIntervalSprintsCommand(
            planningInterval.Id,
            teamId,
            new Dictionary<Guid, Guid?>
            {
                { iterationId, nonExistentSprintId }
            });

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenSprintIsNotOfTypeSprint()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2024, 1, 1), new LocalDate(2024, 3, 31));
        var teamId = Guid.NewGuid();

        var planningInterval = _planningIntervalFaker
            .WithDateRange(piDates)
            .WithTeams(teamId)
            .WithIterations(piDates, 2, "Iteration ")
            .Generate();

        var iteration = _iterationFaker.AsIteration().WithTeamId(teamId).Generate(); // Not a sprint!

        _dbContext.AddPlanningInterval(planningInterval);
        _dbContext.AddIteration(iteration);

        var iterationId = planningInterval.Iterations.First().Id;

        var command = new MapPlanningIntervalSprintsCommand(
            planningInterval.Id,
            teamId,
            new Dictionary<Guid, Guid?>
            {
                { iterationId, iteration.Id }
            });

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("type Sprint");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenSprintBelongsToDifferentTeam()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2024, 1, 1), new LocalDate(2024, 3, 31));
        var team1Id = Guid.NewGuid();
        var team2Id = Guid.NewGuid();

        var planningInterval = _planningIntervalFaker
            .WithDateRange(piDates)
            .WithTeams(team1Id)
            .WithIterations(piDates, 2, "Iteration ")
            .Generate();

        var sprint = _iterationFaker.AsSprint().WithTeamId(team2Id).Generate(); // Different team!

        _dbContext.AddPlanningInterval(planningInterval);
        _dbContext.AddIteration(sprint);

        var iterationId = planningInterval.Iterations.First().Id;

        var command = new MapPlanningIntervalSprintsCommand(
            planningInterval.Id,
            team1Id,
            new Dictionary<Guid, Guid?>
            {
                { iterationId, sprint.Id }
            });

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("team");
    }

    [Fact]
    public async Task Handle_IsIdempotent_WhenCalledMultipleTimes()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2024, 1, 1), new LocalDate(2024, 3, 31));
        var teamId = Guid.NewGuid();

        var planningInterval = _planningIntervalFaker
            .WithDateRange(piDates)
            .WithTeams(teamId)
            .WithIterations(piDates, 2, "Iteration ")
            .Generate();

        var sprint = _iterationFaker.AsSprint().WithTeamId(teamId).Generate();

        _dbContext.AddPlanningInterval(planningInterval);
        _dbContext.AddIteration(sprint);

        var iterationId = planningInterval.Iterations.First().Id;

        var command = new MapPlanningIntervalSprintsCommand(
            planningInterval.Id,
            teamId,
            new Dictionary<Guid, Guid?>
            {
                { iterationId, sprint.Id }
            });

        // Act
        var result1 = await _handler.Handle(command, TestContext.Current.CancellationToken);
        var result2 = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        planningInterval.IterationSprints.Should().HaveCount(1);
        _dbContext.SaveChangesCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldOnlyAffectSpecifiedTeam_WhenMultipleTeamsExist()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2024, 1, 1), new LocalDate(2024, 3, 31));
        var team1Id = Guid.NewGuid();
        var team2Id = Guid.NewGuid();

        var planningInterval = _planningIntervalFaker
            .WithDateRange(piDates)
            .WithTeams(team1Id, team2Id)
            .WithIterations(piDates, 2, "Iteration ")
            .Generate();

        var team1Sprint = _iterationFaker.AsSprint().WithTeamId(team1Id).Generate();
        var team2Sprint = _iterationFaker.AsSprint().WithTeamId(team2Id).Generate();

        _dbContext.AddPlanningInterval(planningInterval);
        _dbContext.AddIteration(team1Sprint);
        _dbContext.AddIteration(team2Sprint);

        var iterationId = planningInterval.Iterations.First().Id;

        // Map both teams' sprints
        planningInterval.MapSprintToIteration(iterationId, team1Sprint, EventActor.System, Now);
        planningInterval.MapSprintToIteration(iterationId, team2Sprint, EventActor.System, Now);
        planningInterval.IterationSprints.Should().HaveCount(2);

        // Set up Sprint navigation properties
        foreach (var mapping in planningInterval.IterationSprints)
        {
            if (mapping.SprintId == team1Sprint.Id)
                mapping.SetPrivate(m => m.Sprint, team1Sprint);
            if (mapping.SprintId == team2Sprint.Id)
                mapping.SetPrivate(m => m.Sprint, team2Sprint);
        }

        // Sync only team1 with empty mappings (should unmap team1's sprint only)
        var command = new MapPlanningIntervalSprintsCommand(
            planningInterval.Id,
            team1Id,
            []);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        planningInterval.IterationSprints.Should().HaveCount(1, "only team1's sprint should be unmapped");
        planningInterval.IterationSprints.First().SprintId.Should().Be(team2Sprint.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
