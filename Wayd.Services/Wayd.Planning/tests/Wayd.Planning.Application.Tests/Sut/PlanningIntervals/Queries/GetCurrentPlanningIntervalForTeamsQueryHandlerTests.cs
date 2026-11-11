using FluentAssertions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Models;
using Wayd.Planning.Application.PlanningIntervals.Queries;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Tests.Data;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals.Queries;

public sealed class GetCurrentPlanningIntervalForTeamsQueryHandlerTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 10, 9, 0);
    private static readonly LocalDate Today = new(2026, 11, 10);

    private readonly FakePlanningDbContext _dbContext = new();
    private readonly PlanningTeam _teamOfTeams = new PlanningTeamFaker(TeamType.TeamOfTeams).Generate();
    private readonly PlanningTeam _team = new PlanningTeamFaker(TeamType.Team).Generate();
    private readonly PlanningTeam _otherTeam = new PlanningTeamFaker(TeamType.Team).Generate();

    private GetCurrentPlanningIntervalForTeamsQueryHandler CreateSut() =>
        new(_dbContext, new TestingDateTimeProvider(new FakeClock(Now)));

    private GetCurrentPlanningIntervalForTeamsQuery Query(bool includeObjectives = true) =>
        new(_teamOfTeams.Id, [_teamOfTeams.Id, _team.Id], Today, includeObjectives);

    private PlanningInterval AddInterval(string name, LocalDate start, LocalDate end, params Guid[] teamIds)
    {
        var planningInterval = new PlanningIntervalFaker()
            .WithName(name)
            .WithDateRange(new LocalDateRange(start, end))
            .WithTeams(teamIds)
            .Generate();
        _dbContext.AddPlanningInterval(planningInterval);
        return planningInterval;
    }

    private void AddObjective(PlanningInterval planningInterval, PlanningTeam team, ObjectiveStatus status, HealthStatus? health, bool expired = false)
    {
        var objective = new PlanningIntervalObjectiveFaker(planningInterval.Id, team, status, false).Generate();
        if (health is { } healthStatus)
        {
            var healthCheck = new PlanningIntervalObjectiveHealthCheckFaker(objective.Id, Now.Minus(Duration.FromDays(1)))
                .WithStatus(healthStatus)
                .WithExpiration(expired ? Now.Minus(Duration.FromMinutes(1)) : Now.Plus(Duration.FromDays(7)))
                .Generate();
            objective.SetPrivateField("_healthChecks", new List<PlanningIntervalObjectiveHealthCheck> { healthCheck });
        }
        _dbContext.AddPlanningIntervalObjective(objective);
    }

    [Fact]
    public async Task Handle_PrefersTheIntervalTheTeamOfTeamsIsIn_AndCountsOpenObjectivesAtRisk()
    {
        // Arrange
        var teamsOnly = AddInterval("Teams only", Today.PlusDays(-30), Today.PlusDays(30), _team.Id);
        var train = AddInterval("Train", Today.PlusDays(-10), Today.PlusDays(50), _teamOfTeams.Id, _team.Id);
        AddInterval("Last", Today.PlusDays(-90), Today.PlusDays(-1), _teamOfTeams.Id);

        AddObjective(train, _team, ObjectiveStatus.InProgress, HealthStatus.AtRisk);
        AddObjective(train, _teamOfTeams, ObjectiveStatus.NotStarted, HealthStatus.Unhealthy);
        AddObjective(train, _team, ObjectiveStatus.InProgress, HealthStatus.Healthy);
        AddObjective(train, _team, ObjectiveStatus.InProgress, HealthStatus.Unhealthy, expired: true);
        AddObjective(train, _team, ObjectiveStatus.InProgress, null);
        AddObjective(train, _team, ObjectiveStatus.Completed, HealthStatus.AtRisk);
        AddObjective(train, _otherTeam, ObjectiveStatus.InProgress, HealthStatus.AtRisk);
        AddObjective(teamsOnly, _team, ObjectiveStatus.InProgress, HealthStatus.AtRisk);

        // Act
        var result = await CreateSut().Handle(Query(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.PlanningInterval.Id.Should().Be(train.Id);
        result.PlanningInterval.Key.Should().Be(train.Key);
        result.PlanningInterval.Name.Should().Be("Train");
        result.ObjectivesAtRiskCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithoutObjectives_ReturnsTheIntervalUncounted()
    {
        // Arrange
        var train = AddInterval("Train", Today.PlusDays(-10), Today.PlusDays(50), _teamOfTeams.Id);
        AddObjective(train, _teamOfTeams, ObjectiveStatus.InProgress, HealthStatus.AtRisk);

        // Act
        var result = await CreateSut().Handle(Query(includeObjectives: false), TestContext.Current.CancellationToken);

        // Assert
        result!.PlanningInterval.Id.Should().Be(train.Id);
        result.ObjectivesAtRiskCount.Should().BeNull();
    }

    [Fact]
    public async Task Handle_OnlyAChildTeamInAnInterval_ReturnsThatInterval()
    {
        // Arrange
        var teamsOnly = AddInterval("Teams only", Today, Today.PlusDays(30), _team.Id);

        // Act
        var result = await CreateSut().Handle(Query(), TestContext.Current.CancellationToken);

        // Assert
        result!.PlanningInterval.Id.Should().Be(teamsOnly.Id);
        result.ObjectivesAtRiskCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_NoIntervalRunningForTheTeams_ReturnsNull()
    {
        // Arrange
        AddInterval("Ended", Today.PlusDays(-60), Today.PlusDays(-1), _teamOfTeams.Id);
        AddInterval("Next", Today.PlusDays(1), Today.PlusDays(60), _team.Id);
        AddInterval("Elsewhere", Today.PlusDays(-5), Today.PlusDays(5), _otherTeam.Id);

        // Act
        var result = await CreateSut().Handle(Query(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }
}
