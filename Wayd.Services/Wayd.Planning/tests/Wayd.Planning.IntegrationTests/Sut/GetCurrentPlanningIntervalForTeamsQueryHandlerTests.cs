using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models;
using Wayd.Common.Models;
using Wayd.Planning.Application.PlanningIntervals.Queries;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.IntegrationTests.Infrastructure;

namespace Wayd.Planning.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server: the query orders intervals by whether a team takes part and reads each
/// objective's current health check inside the projection, which only a real provider translates.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetCurrentPlanningIntervalForTeamsQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_PrefersTheTeamOfTeamsInterval_AndCountsObjectivesAtRiskByCurrentHealth()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var now = SqlServerDbContextFixture.FixedNow;
        var today = now.InUtc().Date;

        Guid trainIntervalId;
        Guid teamOfTeamsId;
        Guid teamId;
        await using (var seed = _fixture.CreateContext())
        {
            var teamOfTeams = await PlanningSeed.TeamOfTeams(seed, ct);
            var team = await PlanningSeed.Team(seed, ct);
            var reporter = Employee.Create(new PersonName("Ada", null, "Lovelace"), "E" + Guid.NewGuid().ToString("N")[..11], now,
                new EmailAddress($"ada.{Guid.NewGuid():N}@acme.example"), null, null, null, null, true, null, EventActor.System, now);
            seed.Employees.Add(reporter);

            var teamsOnly = PlanningInterval.Create($"Teams PI {Guid.NewGuid():N}"[..20], null,
                new LocalDateRange(today.PlusDays(-20), today.PlusDays(40)), 2, "Iteration ", EventActor.System, now).Value;
            teamsOnly.ManageTeams([team.Id], EventActor.System, now);

            var train = PlanningInterval.Create($"Train PI {Guid.NewGuid():N}"[..20], null,
                new LocalDateRange(today.PlusDays(-10), today.PlusDays(50)), 2, "Iteration ", EventActor.System, now).Value;
            train.ManageTeams([teamOfTeams.Id, team.Id], EventActor.System, now);
            var atRisk = train.CreateObjective(team, "Ship it", null, false, null, null, 1, EventActor.System, now).Value;
            atRisk.AddHealthCheck(HealthStatus.Healthy, reporter.Id, now.Plus(Duration.FromDays(1)), null, EventActor.System, now.Minus(Duration.FromDays(2)));
            atRisk.AddHealthCheck(HealthStatus.AtRisk, reporter.Id, now.Plus(Duration.FromDays(7)), null, EventActor.System, now.Minus(Duration.FromHours(1)));
            var healthy = train.CreateObjective(teamOfTeams, "Plan it", null, false, null, null, 2, EventActor.System, now).Value;
            healthy.AddHealthCheck(HealthStatus.Healthy, reporter.Id, now.Plus(Duration.FromDays(7)), null, EventActor.System, now);
            train.CreateObjective(team, "Unchecked", null, false, null, null, 3, EventActor.System, now);

            seed.PlanningIntervals.AddRange(teamsOnly, train);
            await seed.SaveChangesAsync(ct);

            trainIntervalId = train.Id;
            teamOfTeamsId = teamOfTeams.Id;
            teamId = team.Id;
        }

        await using var context = _fixture.CreateContext();
        var handler = new GetCurrentPlanningIntervalForTeamsQueryHandler(context, SqlServerDbContextFixture.DateTimeProvider(now));

        // Act
        var result = await handler.Handle(new GetCurrentPlanningIntervalForTeamsQuery(teamOfTeamsId, [teamOfTeamsId, teamId], today, IncludeObjectives: true), ct);

        // Assert
        result.Should().NotBeNull();
        result!.PlanningInterval.Id.Should().Be(trainIntervalId);
        result.ObjectivesAtRiskCount.Should().Be(1);
    }
}
