using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Forecasting;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Work.Domain.Models;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because a forecast is built from queries the fakes never translate:
/// the dependency walk, the descendant walk, the backlog ordering columns, the throughput window's
/// instant comparisons, and the team projection. One item finishes per day in the seeded history,
/// so the item at backlog position n finishes on day n in every trial.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetWorkItemForecastQueryTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDate _start = ForecastSeeder.Start;

    private readonly SqlServerDbContextFixture _fixture = fixture;
    private readonly ForecastSeeder _seed = new(fixture);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<WorkItemForecastDto> Forecast(string key, ForecastOptions? options = null)
    {
        MapsterConfiguration.Ensure();
        await using var accessor = new WaydDbContextAccessor(_fixture);
        var handler = new GetWorkItemForecastQueryHandler(
            accessor.Context,
            Mock.Of<IDateTimeProvider>(p => p.Now == ForecastSeeder.Now),
            NullLogger<GetWorkItemForecastQueryHandler>.Instance);

        var forecast = await handler.Handle(new GetWorkItemForecastQuery(ForecastSeeder.WorkspaceKey, new WorkItemKey(key), options: options), Ct);
        return forecast!;
    }

    [Theory]
    [InlineData(true, 3)]
    [InlineData(false, 5)]
    public async Task Handle_PlacesTheItemInItsTeamsBacklog(bool startedWorkFirst, int expectedPosition)
    {
        // Arrange — two active and two proposed items ahead by rank, then an active target
        await _seed.Reset(Ct);
        var team = await _seed.AddTeam(Ct);
        await _seed.AddHistory(team.Id, Ct);
        await _seed.AddItem(team.Id, Ct, WorkStatusCategory.Active, stackRank: 1);
        await _seed.AddItem(team.Id, Ct, WorkStatusCategory.Active, stackRank: 2);
        await _seed.AddItem(team.Id, Ct, stackRank: 3);
        await _seed.AddItem(team.Id, Ct, stackRank: 4);
        var target = await _seed.AddItem(team.Id, Ct, WorkStatusCategory.Active, stackRank: 5);

        // Act
        var forecast = await Forecast(target.Key, new ForecastOptions { StartedWorkFirst = startedWorkFirst });

        // Assert
        forecast.Outcome.Id.Should().Be((int)WorkItemForecastOutcome.Forecast);
        forecast.BacklogPosition.Should().Be(expectedPosition);
        forecast.Percentiles.Should().OnlyContain(p => p.Date == _start.PlusDays(expectedPosition - 1));
        var teamDto = forecast.Teams.Should().ContainSingle().Subject;
        teamDto.Team.Id.Should().Be(team.Id);
        teamDto.ItemsCompleted.Should().Be(90);
    }

    [Fact]
    public async Task Handle_PortfolioItem_FollowsDescendantsAndDependencies()
    {
        // Arrange
        await _seed.Reset(Ct);
        var teamA = await _seed.AddTeam(Ct);
        var teamB = await _seed.AddTeam(Ct);
        await _seed.AddHistory(teamA.Id, Ct);
        await _seed.AddHistory(teamB.Id, Ct);

        var epic = await _seed.AddItem(null, Ct, epic: true);
        var nestedEpic = await _seed.AddItem(null, Ct, epic: true, parentId: epic.Id);
        await _seed.AddItem(teamA.Id, Ct, stackRank: 1, parentId: nestedEpic.Id);
        var waiting = await _seed.AddItem(teamA.Id, Ct, stackRank: 2, parentId: epic.Id);
        await _seed.AddItem(teamA.Id, Ct, WorkStatusCategory.Done, done: ForecastSeeder.Now.Minus(Duration.FromDays(200)), parentId: epic.Id);

        for (var rank = 1; rank <= 3; rank++)
            await _seed.AddItem(teamB.Id, Ct, stackRank: rank);
        var predecessor = await _seed.AddItem(teamB.Id, Ct, stackRank: 4);
        var removedPredecessor = await _seed.AddItem(teamB.Id, Ct, WorkStatusCategory.Removed, done: ForecastSeeder.Now.Minus(Duration.FromDays(3)));
        var donePredecessor = await _seed.AddItem(teamB.Id, Ct, WorkStatusCategory.Done, done: ForecastSeeder.Now.Minus(Duration.FromDays(150)));
        await _seed.AddDependency(predecessor.Id, waiting.Id, Ct);
        await _seed.AddDependency(removedPredecessor.Id, waiting.Id, Ct);
        await _seed.AddDependency(donePredecessor.Id, waiting.Id, Ct);

        // Act
        var forecast = await Forecast(epic.Key);

        // Assert
        forecast.Outcome.Id.Should().Be((int)WorkItemForecastOutcome.Forecast);
        forecast.RemainingWorkItems.Should().Be(2);
        forecast.Percentiles.Should().OnlyContain(p => p.Date == _start.PlusDays(3));
        forecast.Teams.Select(t => t.Team.Id).Should().BeEquivalentTo([teamA.Id, teamB.Id]);

        var dependency = forecast.Dependencies.Should().ContainSingle().Subject;
        dependency.Predecessor.Key.Should().Be(predecessor.Key);
        dependency.Predecessor.WorkspaceKey.Should().Be(ForecastSeeder.WorkspaceKey);
        dependency.ShareOfTrialsSettingFinish.Should().Be(1);

        var ignored = forecast.IgnoredDependencies.Should().ContainSingle().Subject;
        ignored.Predecessor.Key.Should().Be(removedPredecessor.Key);
        ignored.Reason.Id.Should().Be((int)IgnoredDependencyReason.PredecessorRemoved);
    }

    [Fact]
    public async Task Handle_TooLittleHistoryInTheWindow_IsNotEnoughHistory()
    {
        // Arrange — plenty finished, but only before a 30-day window
        await _seed.Reset(Ct);
        var team = await _seed.AddTeam(Ct);
        for (var day = 40; day < 60; day++)
            await _seed.AddItem(team.Id, Ct, WorkStatusCategory.Done, done: ForecastSeeder.Now.Minus(Duration.FromDays(day)));
        var target = await _seed.AddItem(team.Id, Ct, stackRank: 1);

        // Act
        var forecast = await Forecast(target.Key, new ForecastOptions { LookbackDays = 30 });

        // Assert
        forecast.Outcome.Id.Should().Be((int)WorkItemForecastOutcome.NotEnoughHistory);
        forecast.Teams.Should().ContainSingle().Which.ItemsCompleted.Should().Be(0);
    }
}
