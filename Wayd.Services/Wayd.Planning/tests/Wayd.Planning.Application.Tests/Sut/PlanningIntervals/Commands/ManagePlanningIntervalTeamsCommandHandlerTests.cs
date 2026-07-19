using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Planning.Application.PlanningIntervals.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals.Commands;

/// <summary>
/// <see cref="ManagePlanningIntervalTeamsCommandHandler"/> assigns teams to a Planning Interval.
/// <c>PlanningIntervalTeam.TeamId</c> is a required FK to the <c>PlanningTeam</c> projection, which is
/// replicated asynchronously from Organization — so the handler must validate that each requested team's
/// projection exists and fail cleanly rather than letting the save FK-fault.
/// </summary>
public sealed class ManagePlanningIntervalTeamsCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _planningDbContext = new();
    private readonly ManagePlanningIntervalTeamsCommandHandler _handler;

    public ManagePlanningIntervalTeamsCommandHandlerTests()
    {
        _handler = new ManagePlanningIntervalTeamsCommandHandler(_planningDbContext, Mock.Of<ILogger<ManagePlanningIntervalTeamsCommandHandler>>());
    }

    public void Dispose() => _planningDbContext.Dispose();

    [Fact]
    public async Task Handle_WhenAllTeamsHaveProjections_AssignsAndSaves()
    {
        // Arrange
        var pi = new PlanningIntervalFaker().WithTeams().Generate();
        _planningDbContext.AddPlanningInterval(pi);

        var teamAId = Guid.CreateVersion7();
        var teamBId = Guid.CreateVersion7();
        _planningDbContext.AddPlanningTeam(new PlanningTeamFaker(TeamType.Team).WithId(teamAId).Generate());
        _planningDbContext.AddPlanningTeam(new PlanningTeamFaker(TeamType.Team).WithId(teamBId).Generate());

        var command = new ManagePlanningIntervalTeamsCommand(pi.Id, [teamAId, teamBId]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        pi.Teams.Select(t => t.TeamId).Should().BeEquivalentTo([teamAId, teamBId]);
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenATeamHasNoProjection_FailsCleanlyWithoutSaving()
    {
        // Arrange — one team is replicated, the other has not landed yet (the async-replication window).
        var pi = new PlanningIntervalFaker().WithTeams().Generate();
        _planningDbContext.AddPlanningInterval(pi);

        var existingTeamId = Guid.CreateVersion7();
        _planningDbContext.AddPlanningTeam(new PlanningTeamFaker(TeamType.Team).WithId(existingTeamId).Generate());

        var missingTeamId = Guid.CreateVersion7(); // no PlanningTeam projection
        var command = new ManagePlanningIntervalTeamsCommand(pi.Id, [existingTeamId, missingTeamId]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert — a clean failure, no FK fault, nothing saved, no partial assignment.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("could not be found");
        pi.Teams.Should().BeEmpty();
        _planningDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenPlanningIntervalNotFound_ReturnsFailure()
    {
        // Arrange — no PI seeded.
        var command = new ManagePlanningIntervalTeamsCommand(Guid.CreateVersion7(), [Guid.CreateVersion7()]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _planningDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithEmptyTeamList_ClearsTeamsAndSaves()
    {
        // Arrange — a PI that already has a team; assigning an empty set removes it. No existence check is
        // needed for removals, so an empty list must not be rejected.
        var existingTeamId = Guid.CreateVersion7();
        var pi = new PlanningIntervalFaker().WithTeams(existingTeamId).Generate();
        _planningDbContext.AddPlanningInterval(pi);

        var command = new ManagePlanningIntervalTeamsCommand(pi.Id, []);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        pi.Teams.Should().BeEmpty();
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }
}
