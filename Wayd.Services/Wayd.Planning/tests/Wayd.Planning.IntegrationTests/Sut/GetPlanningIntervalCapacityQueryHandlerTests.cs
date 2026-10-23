using Moq;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Common.Models;
using Wayd.Planning.Application.PlanningIntervals.Queries;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.IntegrationTests.Infrastructure;

namespace Wayd.Planning.IntegrationTests.Sut;

/// <summary>
/// A planning interval's capacity against a real SQL Server container.
/// </summary>
/// <remarks>
/// The read projects the interval's iterations and its teams through their navigations, which only a real
/// provider translates. Organization's capacity and Work's team days off are answered by the dispatcher.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetPlanningIntervalCapacityQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDate Start = new(2026, 1, 5);
    private static readonly LocalDate End = new(2026, 3, 1);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_GivesEachTeamItsCapacityForThePlanningIntervalAndEachIteration()
    {
        // Arrange — a team with a sprint day off and a half day, and a team of teams with nobody allocated
        var ct = TestContext.Current.CancellationToken;
        var (intervalKey, team, teamOfTeams) = await Seed(ct);
        var dayOff = Start.PlusDays(2);
        var halfDayOff = Start.PlusDays(3);
        var dispatcher = Dispatcher(
            new Dictionary<Guid, IReadOnlyList<TeamDayOff>> { [team.Id] = [new(dayOff), new(halfDayOff, 50)], [Guid.NewGuid()] = [new(Start)] },
            (teamId, index) => teamId == team.Id ? 10m * (index + 1) : 0m);

        // Act
        var capacity = await Handle(intervalKey, dispatcher.Object, ct);

        // Assert
        capacity.Should().NotBeNull();
        capacity!.Start.Should().Be(Start);
        capacity.End.Should().Be(End);
        capacity.Iterations.Should().HaveCount(2).And.BeInAscendingOrder(i => i.Start);

        capacity.Teams.Select(t => t.Team.Id).Should().Equal(team.Id, teamOfTeams.Id);
        var teamRow = capacity.Teams[0];
        teamRow.HasAllocations.Should().BeTrue();
        teamRow.IsActive.Should().BeTrue();
        teamRow.Total.DeliveryPersonDays.Should().Be(10m);
        teamRow.Iterations.Select(i => i.DeliveryPersonDays).Should().Equal(20m, 30m);

        // Each iteration's figures are those of the period asked for at its dates, in date order.
        var asked = (GetTeamsCapacityByPeriodQuery)dispatcher.Invocations
            .Single(i => i.Arguments[0] is GetTeamsCapacityByPeriodQuery).Arguments[0]!;
        asked.Periods.Skip(1).Select(p => (p.Start, p.End))
            .Should().Equal(capacity.Iterations.Select(i => (i.Start, i.End)));
        teamRow.TeamDaysOff.Should().Equal(
            new TeamDayOffDto { Date = dayOff, PercentOff = 100 },
            new TeamDayOffDto { Date = halfDayOff, PercentOff = 50 });

        var groupRow = capacity.Teams[1];
        groupRow.HasAllocations.Should().BeFalse();
        groupRow.TeamDaysOff.Should().BeEmpty();

        dispatcher.Verify(d => d.Send(
            It.Is<GetTeamsCapacityByPeriodQuery>(q =>
                q.TeamIds.Count == 2
                && q.Periods.Count == 3
                && q.Periods[0] == new CapacityPeriod(Start, End, null)
                && q.Periods.Skip(1).Select(p => p.Start).SequenceEqual(capacity.Iterations.Select(i => i.Start))
                && q.TeamDaysOff != null && q.TeamDaysOff.ContainsKey(team.Id)),
            It.IsAny<CancellationToken>()), Times.Once);
        dispatcher.Verify(d => d.Send(new GetTeamDaysOffQuery(Start, End), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithTheFeatureOff_ReturnsNull()
    {
        // Arrange — Organization answers the capacity read with null when the feature is off
        var ct = TestContext.Current.CancellationToken;
        var (intervalKey, _, _) = await Seed(ct);
        var dispatcher = Dispatcher(new Dictionary<Guid, IReadOnlyList<TeamDayOff>>(), (_, _) => 0m);
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsCapacityByPeriodQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<Guid, IReadOnlyList<TeamPeriodCapacity>>?)null);

        // Act
        var capacity = await Handle(intervalKey, dispatcher.Object, ct);

        // Assert
        capacity.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ForAPlanningIntervalThatDoesNotExist_ReturnsNullWithoutReadingCapacity()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var dispatcher = Dispatcher(new Dictionary<Guid, IReadOnlyList<TeamDayOff>>(), (_, _) => 0m);

        // Act
        var capacity = await Handle(int.MaxValue, dispatcher.Object, ct);

        // Assert
        capacity.Should().BeNull();
        dispatcher.Verify(d => d.Send(It.IsAny<GetTeamsCapacityByPeriodQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A planning interval of two four-week iterations with a team and a team of teams.</summary>
    private async Task<(int IntervalKey, PlanningTeam Team, PlanningTeam TeamOfTeams)> Seed(CancellationToken ct)
    {
        await using var seed = _fixture.CreateContext();
        var team = await PlanningSeed.Team(seed, ct);
        var teamOfTeams = await PlanningSeed.TeamOfTeams(seed, ct);

        var interval = PlanningInterval.Create($"Atlas PI {Guid.NewGuid():N}"[..20], null,
            new LocalDateRange(Start, End), 4, "Iteration ", EventActor.System, SqlServerDbContextFixture.FixedNow).Value;
        interval.ManageTeams([teamOfTeams.Id, team.Id], EventActor.System, SqlServerDbContextFixture.FixedNow);
        seed.PlanningIntervals.Add(interval);
        await seed.SaveChangesAsync(ct);

        return (interval.Key, team, teamOfTeams);
    }

    /// <summary>
    /// A dispatcher answering team days off with <paramref name="daysOff"/>, and each team's delivery person-days
    /// in each period with <paramref name="delivery"/> by team id and period index.
    /// </summary>
    private static Mock<IDispatcher> Dispatcher(
        IReadOnlyDictionary<Guid, IReadOnlyList<TeamDayOff>> daysOff,
        Func<Guid, int, decimal> delivery)
    {
        var dispatcher = new Mock<IDispatcher>();
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamDaysOffQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(daysOff);
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsCapacityByPeriodQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetTeamsCapacityByPeriodQuery q, CancellationToken _) =>
                q.TeamIds.ToDictionary(
                    id => id,
                    id => (IReadOnlyList<TeamPeriodCapacity>)[.. q.Periods.Select((p, i) =>
                        new TeamPeriodCapacity(p, delivery(id, i), delivery(id, i), 0m, delivery(id, i) > 0))]));
        return dispatcher;
    }

    private async Task<Application.PlanningIntervals.Dtos.PlanningIntervalCapacityDto?> Handle(int intervalKey, IDispatcher dispatcher, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        var handler = new GetPlanningIntervalCapacityQueryHandler(context, dispatcher);
        return await handler.Handle(new GetPlanningIntervalCapacityQuery(new IdOrKey(intervalKey.ToString())), ct);
    }
}
