using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Forecasting;
using Wayd.Work.Application.WorkTeams.Queries;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkTeams.Queries;

public sealed class GetTeamThroughputForecastQueryTests : IDisposable
{
    private static readonly LocalDate _start = ForecastScenario.Start;

    private readonly ForecastScenario _scenario = new();

    public void Dispose() => _scenario.Dispose();

    private GetTeamThroughputForecastQueryHandler Handler() => new(_scenario.Context, _scenario.DateTimeProvider);

    [Theory]
    [InlineData(true, "active")]
    [InlineData(false, "proposed")]
    public async Task Handle_NamesTheItemReachedInTheOrderAsked(bool startedWorkFirst, string expected)
    {
        // Arrange — one item a day, so one day reaches exactly the first item worked
        var team = _scenario.NewTeam();
        _scenario.AddHistory(team);
        var proposed = _scenario.AddItem(team, stackRank: 1);
        var active = _scenario.AddItem(team, WorkStatusCategory.Active, stackRank: 2);

        // Act
        var result = await Handler().Handle(
            new GetTeamThroughputForecastQuery(team, _start, StartedWorkFirst: startedWorkFirst),
            TestContext.Current.CancellationToken);

        // Assert
        result.Value!.StartedWorkFirst.Should().Be(startedWorkFirst);
        var reached = expected == "active" ? active : proposed;
        result.Value.Percentiles.Should().OnlyContain(p => p.ThroughWorkItem!.Key == reached.Key.Value);
    }

    [Fact]
    public async Task Handle_ForecastsTheItemsFinishedByTheTargetDate()
    {
        // Arrange — one item a day, so five days finish five items in every trial
        var team = _scenario.NewTeam();
        _scenario.AddHistory(team);
        var backlog = Enumerable.Range(1, 8).Select(rank => _scenario.AddItem(team, stackRank: rank)).ToList();

        // Act
        var result = await Handler().Handle(new GetTeamThroughputForecastQuery(team, _start.PlusDays(4)), TestContext.Current.CancellationToken);

        // Assert
        var forecast = result.Value!;
        forecast.Outcome.Id.Should().Be((int)WorkItemForecastOutcome.Forecast);
        forecast.Days.Should().Be(5);
        forecast.BacklogWorkItems.Should().Be(8);
        forecast.Team.Team.Id.Should().Be(team);
        forecast.Team.ItemsCompleted.Should().Be(90);
        forecast.Percentiles.Select(p => p.Confidence).Should().Equal(50, 70, 85, 95);
        forecast.Percentiles.Should().OnlyContain(p => p.WorkItems == 5 && p.ThroughWorkItem!.Key == backlog[4].Key.Value);
        forecast.Histogram.Should().ContainSingle().Which.WorkItems.Should().Be(5);
    }

    [Fact]
    public async Task Handle_MoreThanTheBacklog_HasNoThroughWorkItem()
    {
        // Arrange
        var team = _scenario.NewTeam();
        _scenario.AddHistory(team);
        _scenario.AddItem(team, stackRank: 1);

        // Act
        var result = await Handler().Handle(new GetTeamThroughputForecastQuery(team, _start.PlusDays(4)), TestContext.Current.CancellationToken);

        // Assert
        result.Value!.Percentiles.Should().OnlyContain(p => p.WorkItems == 5 && p.ThroughWorkItem == null);
    }

    [Fact]
    public async Task Handle_SamplesTheRequestedDaysOfHistory()
    {
        // Arrange
        var team = _scenario.NewTeam();
        _scenario.AddHistory(team);

        // Act
        var result = await Handler().Handle(new GetTeamThroughputForecastQuery(team, _start, LookbackDays: 30), TestContext.Current.CancellationToken);

        // Assert
        result.Value!.LookbackDays.Should().Be(30);
        result.Value.Team.ItemsCompleted.Should().Be(30);
    }

    [Fact]
    public async Task Handle_TooLittleHistory_IsNotEnoughHistory()
    {
        // Arrange
        var team = _scenario.NewTeam();
        _scenario.AddHistory(team, days: TeamThroughputSampler.MinimumItemsCompleted - 1);

        // Act
        var result = await Handler().Handle(new GetTeamThroughputForecastQuery(team, _start.PlusDays(10)), TestContext.Current.CancellationToken);

        // Assert
        result.Value!.Outcome.Id.Should().Be((int)WorkItemForecastOutcome.NotEnoughHistory);
        result.Value.Percentiles.Should().BeEmpty();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(730)]
    public async Task Handle_TargetDateOutOfRange_Fails(int offsetDays)
    {
        // Arrange
        var team = _scenario.NewTeam();

        // Act
        var result = await Handler().Handle(new GetTeamThroughputForecastQuery(team, _start.PlusDays(offsetDays)), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_UnknownTeam_ReturnsNull()
    {
        // Act
        var result = await Handler().Handle(new GetTeamThroughputForecastQuery(Guid.NewGuid(), _start), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Handle_CountsOnlyOpenBacklogItems()
    {
        // Arrange
        var team = _scenario.NewTeam();
        _scenario.AddHistory(team);
        _scenario.AddItem(team, stackRank: 1);
        _scenario.AddItem(team, WorkStatusCategory.Active, stackRank: 2);
        _scenario.AddItem(team, WorkStatusCategory.Removed, stackRank: 3);
        _scenario.AddItem(team, type: _scenario.Epic);

        // Act
        var result = await Handler().Handle(new GetTeamThroughputForecastQuery(team, _start), TestContext.Current.CancellationToken);

        // Assert
        result.Value!.BacklogWorkItems.Should().Be(2);
    }
}
