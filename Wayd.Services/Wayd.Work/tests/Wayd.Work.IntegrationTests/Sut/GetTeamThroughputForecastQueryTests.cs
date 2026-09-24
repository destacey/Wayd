using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkTeams.Queries;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server so the team is resolved by code, the backlog is read and ordered,
/// and keys are fetched for the items the confidence levels reach, all through translated queries.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetTeamThroughputForecastQueryTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;
    private readonly ForecastSeeder _seed = new(fixture);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_CountsTheItemsFinishedAndNamesTheOneReached(bool startedWorkFirst)
    {
        // Arrange — one item a day, so one day finishes one item in every trial
        MapsterConfiguration.Ensure();
        await _seed.Reset(Ct);
        var team = await _seed.AddTeam(Ct);
        await _seed.AddHistory(team.Id, Ct);
        var firstRanked = await _seed.AddItem(team.Id, Ct, stackRank: 1);
        await _seed.AddItem(team.Id, Ct, stackRank: 2);
        var active = await _seed.AddItem(team.Id, Ct, WorkStatusCategory.Active, stackRank: 3);

        await using var accessor = new WaydDbContextAccessor(_fixture);
        var handler = new GetTeamThroughputForecastQueryHandler(
            accessor.Context,
            Mock.Of<IDateTimeProvider>(p => p.Now == ForecastSeeder.Now));

        // Act
        var result = await handler.Handle(
            new GetTeamThroughputForecastQuery(team.Code, ForecastSeeder.Start, StartedWorkFirst: startedWorkFirst),
            Ct);

        // Assert — started work first puts the active item ahead of both proposed ones
        var forecast = result.Value!;
        forecast.Outcome.Id.Should().Be((int)WorkItemForecastOutcome.Forecast);
        forecast.BacklogWorkItems.Should().Be(3);
        forecast.Team.ItemsCompleted.Should().Be(90);
        forecast.Percentiles.Should().OnlyContain(p => p.WorkItems == 1);
        var expected = startedWorkFirst ? active.Key : firstRanked.Key;
        forecast.Percentiles.Should().OnlyContain(p => p.ThroughWorkItem!.Key == expected);
    }
}
