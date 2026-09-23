using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Forecasting;
using Wayd.Work.Domain.Models;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkItems.Forecasting;

public sealed class WorkItemForecastBuilderTests : IDisposable
{
    private static readonly LocalDate _start = ForecastScenario.Start;

    private readonly ForecastScenario _scenario = new();

    public void Dispose() => _scenario.Dispose();

    private Task<WorkItemForecastDto> Forecast(params WorkItem[] workItems) => Forecast(workItems, targetDate: null);

    private Task<WorkItemForecastDto> Forecast(WorkItem[] workItems, LocalDate? targetDate, ForecastOptions? options = null) =>
        new WorkItemForecastBuilder(_scenario.Context).Build(
            [.. workItems.Select(w => w.Id)],
            Guid.Parse("7d3f2a8e-5b1c-4e6f-9a0d-2c4b6e8f1a3d"),
            ForecastScenario.Now,
            targetDate,
            options ?? ForecastOptions.Default,
            TestContext.Current.CancellationToken);

    private static WorkItemForecastOutcome Outcome(WorkItemForecastDto forecast) => (WorkItemForecastOutcome)forecast.Outcome.Id;

    [Fact]
    public async Task Build_FinishesOnTheDayItsBacklogPositionIsReached()
    {
        // Arrange
        var team = _scenario.NewTeam();
        _scenario.AddHistory(team);
        _scenario.AddItem(team, stackRank: 10);
        _scenario.AddItem(team, stackRank: 20);
        var target = _scenario.AddItem(team, stackRank: 30);

        // Act
        var forecast = await Forecast(target);

        // Assert
        Outcome(forecast).Should().Be(WorkItemForecastOutcome.Forecast);
        forecast.ForecastStart.Should().Be(_start);
        forecast.BacklogPosition.Should().Be(3);
        forecast.RemainingWorkItems.Should().Be(1);
        var teamDto = forecast.Teams.Should().ContainSingle().Subject;
        teamDto.Team.Id.Should().Be(team);
        teamDto.From.Should().Be(new LocalDate(2026, 6, 24));
        teamDto.To.Should().Be(new LocalDate(2026, 9, 21));
        teamDto.ItemsCompleted.Should().Be(90);
        forecast.Percentiles.Select(p => p.Confidence).Should().Equal(50, 70, 85, 95);
        forecast.Percentiles.Should().OnlyContain(p => p.Date == _start.PlusDays(2));
        forecast.Histogram.Should().ContainSingle().Which.Should().Be(new ForecastHistogramBucketDto { Date = _start.PlusDays(2), Trials = forecast.Trials });
        forecast.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Build_EqualRanks_OrderByCreated()
    {
        // Arrange
        var team = _scenario.NewTeam();
        _scenario.AddHistory(team);
        var target = _scenario.AddItem(team, stackRank: 10, created: Instant.FromUtc(2026, 2, 1, 0, 0));
        _scenario.AddItem(team, stackRank: 10, created: Instant.FromUtc(2026, 1, 1, 0, 0));

        // Act
        var forecast = await Forecast(target);

        // Assert
        forecast.BacklogPosition.Should().Be(2);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(10, 1)]
    public async Task Build_ReportsTheChanceOfFinishingByTheTargetDate(int targetDay, double expected)
    {
        // Arrange — the target finishes on its second day in every trial
        var team = _scenario.NewTeam();
        _scenario.AddHistory(team);
        _scenario.AddItem(team, stackRank: 1);
        var target = _scenario.AddItem(team, stackRank: 2);
        var targetDate = _start.PlusDays(targetDay - 1);

        // Act
        var forecast = await Forecast([target], targetDate);

        // Assert
        forecast.TargetDate.Should().Be(targetDate);
        forecast.ChanceOfFinishingByTargetDate.Should().Be(expected);
    }

    [Fact]
    public async Task Build_TargetDateInThePast_HasNoChance()
    {
        // Arrange
        var team = _scenario.NewTeam();
        _scenario.AddHistory(team);
        var target = _scenario.AddItem(team, stackRank: 1);

        // Act
        var forecast = await Forecast([target], _start.PlusDays(-3));

        // Assert
        forecast.ChanceOfFinishingByTargetDate.Should().Be(0);
    }

    [Fact]
    public async Task Build_WaitsForAPredecessorOnAnotherTeam()
    {
        // Arrange
        var teamA = _scenario.NewTeam();
        var teamB = _scenario.NewTeam();
        _scenario.AddHistory(teamA);
        _scenario.AddHistory(teamB);
        var target = _scenario.AddItem(teamA, stackRank: 1);
        for (var rank = 1; rank <= 4; rank++)
            _scenario.AddItem(teamB, stackRank: rank);
        var predecessor = _scenario.AddItem(teamB, stackRank: 5);
        _scenario.AddDependency(predecessor, target);

        // Act
        var forecast = await Forecast(target);

        // Assert
        forecast.Percentiles.Should().OnlyContain(p => p.Date == _start.PlusDays(4));
        forecast.Teams.Should().HaveCount(2);
        var dependency = forecast.Dependencies.Should().ContainSingle().Subject;
        dependency.Predecessor.Key.Should().Be(predecessor.Key.Value);
        dependency.Successor.Key.Should().Be(target.Key.Value);
        dependency.ShareOfTrialsSettingFinish.Should().Be(1);
    }

    [Fact]
    public async Task Build_IgnoringDependencies_LeavesPredecessorsOut()
    {
        // Arrange
        var teamA = _scenario.NewTeam();
        var teamB = _scenario.NewTeam();
        _scenario.AddHistory(teamA);
        _scenario.AddHistory(teamB);
        var target = _scenario.AddItem(teamA, stackRank: 1);
        for (var rank = 1; rank <= 4; rank++)
            _scenario.AddItem(teamB, stackRank: rank);
        _scenario.AddDependency(_scenario.AddItem(teamB, stackRank: 5), target);

        // Act
        var forecast = await Forecast([target], targetDate: null, new ForecastOptions { IgnoreDependencies = true });

        // Assert
        forecast.IgnoreDependencies.Should().BeTrue();
        forecast.Percentiles.Should().OnlyContain(p => p.Date == _start);
        forecast.Dependencies.Should().BeEmpty();
        forecast.Teams.Should().ContainSingle().Which.Team.Id.Should().Be(teamA);
    }

    [Fact]
    public async Task Build_SamplesTheRequestedDaysOfHistory()
    {
        // Arrange
        var team = _scenario.NewTeam();
        _scenario.AddHistory(team);
        var target = _scenario.AddItem(team, stackRank: 1);

        // Act
        var forecast = await Forecast([target], targetDate: null, new ForecastOptions { LookbackDays = 30 });

        // Assert
        forecast.LookbackDays.Should().Be(30);
        var teamDto = forecast.Teams.Should().ContainSingle().Subject;
        teamDto.From.Should().Be(new LocalDate(2026, 8, 23));
        teamDto.ItemsCompleted.Should().Be(30);
    }

    [Theory]
    [InlineData(WorkStatusCategory.Done)]
    [InlineData(WorkStatusCategory.Removed)]
    public async Task Build_ClosedPredecessor_DoesNotHoldTheItemUp(WorkStatusCategory predecessorStatus)
    {
        // Arrange
        var team = _scenario.NewTeam();
        _scenario.AddHistory(team);
        var target = _scenario.AddItem(team, stackRank: 1);
        var predecessor = _scenario.AddItem(team, predecessorStatus, done: ForecastScenario.Now.Minus(Duration.FromDays(200)));
        _scenario.AddDependency(predecessor, target);

        // Act
        var forecast = await Forecast(target);

        // Assert
        Outcome(forecast).Should().Be(WorkItemForecastOutcome.Forecast);
        forecast.Percentiles.Should().OnlyContain(p => p.Date == _start);
        forecast.Dependencies.Should().BeEmpty();
        forecast.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Build_RemovedPredecessor_IsReportedAsAnIgnoredDependency()
    {
        // Arrange
        var team = _scenario.NewTeam();
        _scenario.AddHistory(team);
        var target = _scenario.AddItem(team, stackRank: 1);
        var predecessor = _scenario.AddItem(team, WorkStatusCategory.Removed, done: ForecastScenario.Now.Minus(Duration.FromDays(5)));
        _scenario.AddDependency(predecessor, target);

        // Act
        var forecast = await Forecast(target);

        // Assert
        var ignored = forecast.IgnoredDependencies.Should().ContainSingle().Subject;
        ignored.Predecessor.Key.Should().Be(predecessor.Key.Value);
        ignored.Successor.Key.Should().Be(target.Key.Value);
        ignored.Reason.Id.Should().Be((int)IgnoredDependencyReason.PredecessorRemoved);
    }

    [Fact]
    public async Task Build_DonePredecessor_IsNotReported()
    {
        // Arrange
        var team = _scenario.NewTeam();
        _scenario.AddHistory(team);
        var target = _scenario.AddItem(team, stackRank: 1);
        _scenario.AddDependency(_scenario.AddDoneItem(team), target);

        // Act
        var forecast = await Forecast(target);

        // Assert
        forecast.IgnoredDependencies.Should().BeEmpty();
    }

    [Fact]
    public async Task Build_PredecessorWithoutTeam_BlocksTheForecast()
    {
        // Arrange
        var team = _scenario.NewTeam();
        _scenario.AddHistory(team);
        var target = _scenario.AddItem(team, stackRank: 1);
        var predecessor = _scenario.AddItem(teamId: null);
        _scenario.AddDependency(predecessor, target);

        // Act
        var forecast = await Forecast(target);

        // Assert
        Outcome(forecast).Should().Be(WorkItemForecastOutcome.BlockedByDependency);
        forecast.Percentiles.Should().BeEmpty();
        var issue = forecast.Issues.Should().ContainSingle().Subject;
        issue.WorkItem.Key.Should().Be(predecessor.Key.Value);
        issue.Type.Id.Should().Be((int)ForecastIssueType.NoTeam);
    }

    [Fact]
    public async Task Build_DependencyCycle_IgnoresTheDependencyThatClosesIt()
    {
        // Arrange
        var team = _scenario.NewTeam();
        _scenario.AddHistory(team);
        var target = _scenario.AddItem(team, stackRank: 1);
        var predecessor = _scenario.AddItem(team, stackRank: 2);
        _scenario.AddDependency(predecessor, target);
        _scenario.AddDependency(target, predecessor);

        // Act
        var forecast = await Forecast(target);

        // Assert
        Outcome(forecast).Should().Be(WorkItemForecastOutcome.Forecast);
        forecast.IgnoredDependencies.Should().ContainSingle()
            .Which.Reason.Id.Should().Be((int)IgnoredDependencyReason.ClosesCycle);
        forecast.Dependencies.Should().ContainSingle();
    }

    [Fact]
    public async Task Build_TooLittleHistory_IsNotEnoughHistory()
    {
        // Arrange
        var team = _scenario.NewTeam();
        _scenario.AddHistory(team, days: TeamThroughputSampler.MinimumItemsCompleted - 1);
        var target = _scenario.AddItem(team, stackRank: 1);

        // Act
        var forecast = await Forecast(target);

        // Assert
        Outcome(forecast).Should().Be(WorkItemForecastOutcome.NotEnoughHistory);
        forecast.Teams.Should().ContainSingle().Which.ItemsCompleted.Should().Be(TeamThroughputSampler.MinimumItemsCompleted - 1);
        forecast.Issues.Should().ContainSingle().Which.Type.Id.Should().Be((int)ForecastIssueType.NotEnoughHistory);
    }

    [Fact]
    public async Task Build_ItemWithoutTeam_CannotBeForecast()
    {
        // Arrange
        var target = _scenario.AddItem(teamId: null);

        // Act
        var forecast = await Forecast(target);

        // Assert
        Outcome(forecast).Should().Be(WorkItemForecastOutcome.CannotForecast);
        forecast.BacklogPosition.Should().BeNull();
        forecast.Teams.Should().BeEmpty();
        forecast.Issues.Should().ContainSingle().Which.Type.Id.Should().Be((int)ForecastIssueType.NoTeam);
    }

    [Fact]
    public async Task Build_UnrankedItems_FollowRankedOnesOldestFirst()
    {
        // Arrange — the rank the Azure DevOps sync gives an item with none
        const double unranked = 999_999_999_999D;
        var team = _scenario.NewTeam();
        _scenario.AddHistory(team);
        _scenario.AddItem(team, stackRank: 5, created: Instant.FromUtc(2026, 3, 1, 0, 0));
        var target = _scenario.AddItem(team, stackRank: unranked, created: Instant.FromUtc(2026, 2, 1, 0, 0));
        _scenario.AddItem(team, stackRank: unranked, created: Instant.FromUtc(2026, 1, 1, 0, 0));

        // Act
        var forecast = await Forecast(target);

        // Assert
        Outcome(forecast).Should().Be(WorkItemForecastOutcome.Forecast);
        forecast.BacklogPosition.Should().Be(3);
        forecast.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Build_EverythingDone_IsAlreadyDone()
    {
        // Arrange
        var team = _scenario.NewTeam();
        var first = _scenario.AddDoneItem(team);
        var second = _scenario.AddDoneItem(team);

        // Act
        var forecast = await Forecast(first, second);

        // Assert
        Outcome(forecast).Should().Be(WorkItemForecastOutcome.AlreadyDone);
    }

    [Fact]
    public async Task Build_NoWorkItems_HasNothingRemaining()
    {
        // Act
        var forecast = await Forecast();

        // Assert
        Outcome(forecast).Should().Be(WorkItemForecastOutcome.NoRemainingWork);
        forecast.RemainingWorkItems.Should().Be(0);
    }

    [Fact]
    public async Task Build_SameInputsOnTheSameDay_GiveTheSameForecast()
    {
        // Arrange
        var team = _scenario.NewTeam();
        for (var day = 1; day <= 90; day += 3)
        {
            _scenario.AddItem(team, WorkStatusCategory.Done, done: ForecastScenario.Now.Minus(Duration.FromDays(day)));
            _scenario.AddItem(team, WorkStatusCategory.Done, done: ForecastScenario.Now.Minus(Duration.FromDays(day)));
        }
        for (var rank = 1; rank <= 20; rank++)
            _scenario.AddItem(team, stackRank: rank);
        var target = _scenario.AddItem(team, stackRank: 21);

        // Act
        var first = await Forecast(target);
        var second = await Forecast(target);

        // Assert
        second.Percentiles.Should().Equal(first.Percentiles);
        second.Histogram.Should().Equal(first.Histogram);
    }

    [Fact]
    public async Task Build_PortfolioItem_FinishesWithItsLastOpenBacklogDescendant()
    {
        // Arrange
        var teamA = _scenario.NewTeam();
        var teamB = _scenario.NewTeam();
        _scenario.AddHistory(teamA);
        _scenario.AddHistory(teamB);
        var epic = _scenario.AddItem(teamId: null, type: _scenario.Epic);
        var feature = _scenario.AddItem(teamId: null, type: _scenario.Feature, parentId: epic.Id);
        _scenario.AddItem(teamA, stackRank: 1);
        _scenario.AddItem(teamA, stackRank: 2, parentId: feature.Id);
        for (var rank = 1; rank <= 3; rank++)
            _scenario.AddItem(teamB, stackRank: rank);
        _scenario.AddItem(teamB, stackRank: 4, parentId: epic.Id);
        _scenario.AddDoneItem(teamB, parentId: feature.Id);

        // Act
        var forecast = await Forecast(epic);

        // Assert
        Outcome(forecast).Should().Be(WorkItemForecastOutcome.Forecast);
        forecast.RemainingWorkItems.Should().Be(2);
        forecast.BacklogPosition.Should().BeNull();
        forecast.Percentiles.Should().OnlyContain(p => p.Date == _start.PlusDays(3));
        forecast.Teams.Select(t => t.Team.Id).Should().BeEquivalentTo([teamA, teamB]);
        forecast.ExcludedWorkItems.Should().BeEmpty();
    }

    [Fact]
    public async Task Build_PortfolioItem_ExcludesDescendantsThatCannotBeForecast()
    {
        // Arrange
        var team = _scenario.NewTeam();
        _scenario.AddHistory(team);
        var epic = _scenario.AddItem(teamId: null, type: _scenario.Epic);
        _scenario.AddItem(team, stackRank: 1, parentId: epic.Id);
        var withoutTeam = _scenario.AddItem(teamId: null, parentId: epic.Id);

        // Act
        var forecast = await Forecast(epic);

        // Assert
        Outcome(forecast).Should().Be(WorkItemForecastOutcome.Forecast);
        forecast.Percentiles.Should().OnlyContain(p => p.Date == _start);
        forecast.ExcludedWorkItems.Should().ContainSingle().Which.Key.Should().Be(withoutTeam.Key.Value);
        forecast.Issues.Should().ContainSingle().Which.Type.Id.Should().Be((int)ForecastIssueType.NoTeam);
    }

    [Fact]
    public async Task Build_ManyItemsOnATeam_FinishWithTheFurthestDownTheBacklog()
    {
        // Arrange — uneven history, so trials differ
        var team = _scenario.NewTeam();
        for (var day = 1; day <= 90; day += 3)
        {
            _scenario.AddItem(team, WorkStatusCategory.Done, done: ForecastScenario.Now.Minus(Duration.FromDays(day)));
            _scenario.AddItem(team, WorkStatusCategory.Done, done: ForecastScenario.Now.Minus(Duration.FromDays(day)));
        }
        var epic = _scenario.AddItem(teamId: null, type: _scenario.Epic);
        WorkItem? furthest = null;
        for (var rank = 1; rank <= 25; rank++)
        {
            var inEpic = rank % 2 == 0;
            var item = _scenario.AddItem(team, stackRank: rank, parentId: inEpic ? epic.Id : null);
            if (inEpic)
                furthest = item;
        }

        // Act
        var epicForecast = await Forecast(epic);
        var furthestForecast = await Forecast(furthest!);

        // Assert
        epicForecast.RemainingWorkItems.Should().Be(12);
        epicForecast.Histogram.Should().Equal(furthestForecast.Histogram);
        epicForecast.Percentiles.Should().Equal(furthestForecast.Percentiles);
    }

    [Fact]
    public async Task Build_LinkedItemWaitingLonger_SetsTheFinishOverUnlinkedOnes()
    {
        // Arrange — the linked story sits first on team A but waits on day 5 of team B
        var teamA = _scenario.NewTeam();
        var teamB = _scenario.NewTeam();
        _scenario.AddHistory(teamA);
        _scenario.AddHistory(teamB);
        var epic = _scenario.AddItem(teamId: null, type: _scenario.Epic);
        var linked = _scenario.AddItem(teamA, stackRank: 1, parentId: epic.Id);
        _scenario.AddItem(teamA, stackRank: 2, parentId: epic.Id);
        _scenario.AddItem(teamA, stackRank: 3, parentId: epic.Id);
        for (var rank = 1; rank <= 4; rank++)
            _scenario.AddItem(teamB, stackRank: rank);
        _scenario.AddDependency(_scenario.AddItem(teamB, stackRank: 5), linked);

        // Act
        var forecast = await Forecast(epic);

        // Assert
        forecast.RemainingWorkItems.Should().Be(3);
        forecast.Percentiles.Should().OnlyContain(p => p.Date == _start.PlusDays(4));
        forecast.Dependencies.Should().ContainSingle().Which.ShareOfTrialsSettingFinish.Should().Be(1);
    }

    [Fact]
    public async Task Build_SeveralPortfolioItems_ExpandTogetherAndCountSharedWorkOnce()
    {
        // Arrange
        var team = _scenario.NewTeam();
        _scenario.AddHistory(team);
        var epic = _scenario.AddItem(teamId: null, type: _scenario.Epic);
        var feature = _scenario.AddItem(teamId: null, type: _scenario.Feature, parentId: epic.Id);
        var otherEpic = _scenario.AddItem(teamId: null, type: _scenario.Epic);
        _scenario.AddItem(team, stackRank: 1, parentId: feature.Id);
        _scenario.AddItem(team, stackRank: 2, parentId: epic.Id);
        _scenario.AddItem(team, stackRank: 3, parentId: otherEpic.Id);

        // Act
        var forecast = await Forecast(epic, feature, otherEpic);

        // Assert
        forecast.RemainingWorkItems.Should().Be(3);
        forecast.Percentiles.Should().OnlyContain(p => p.Date == _start.PlusDays(2));
    }

    [Fact]
    public async Task Build_PortfolioItemWithNoOpenBacklogDescendants_HasNothingRemaining()
    {
        // Arrange
        var epic = _scenario.AddItem(teamId: null, type: _scenario.Epic);
        _scenario.AddDoneItem(_scenario.NewTeam(), parentId: epic.Id);

        // Act
        var forecast = await Forecast(epic);

        // Assert
        Outcome(forecast).Should().Be(WorkItemForecastOutcome.NoRemainingWork);
        forecast.RemainingWorkItems.Should().Be(0);
    }

    [Fact]
    public async Task Build_PortfolioItemWhoseTeamsLackHistory_IsNotEnoughHistory()
    {
        // Arrange
        var team = _scenario.NewTeam();
        _scenario.AddHistory(team, days: 3);
        var epic = _scenario.AddItem(teamId: null, type: _scenario.Epic);
        _scenario.AddItem(team, stackRank: 1, parentId: epic.Id);
        _scenario.AddItem(team, stackRank: 2, parentId: epic.Id);

        // Act
        var forecast = await Forecast(epic);

        // Assert
        Outcome(forecast).Should().Be(WorkItemForecastOutcome.NotEnoughHistory);
        forecast.ExcludedWorkItems.Should().BeEmpty();
        forecast.Percentiles.Should().BeEmpty();
    }

    [Fact]
    public async Task Build_WorkItemAndItsPortfolioParent_CountTheWorkItemOnce()
    {
        // Arrange
        var team = _scenario.NewTeam();
        _scenario.AddHistory(team);
        var epic = _scenario.AddItem(teamId: null, type: _scenario.Epic);
        var story = _scenario.AddItem(team, stackRank: 1, parentId: epic.Id);
        var other = _scenario.AddItem(team, stackRank: 2);

        // Act
        var forecast = await Forecast(epic, story, other);

        // Assert
        forecast.RemainingWorkItems.Should().Be(2);
        forecast.BacklogPosition.Should().BeNull();
        forecast.Percentiles.Should().OnlyContain(p => p.Date == _start.PlusDays(1));
    }
}
