using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Work.Application.WorkTeams.Queries;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because the handler filters on the work type's tier through navigations and
/// joins work item history, which the fakes do not translate.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetKanbanTeamsWindowMetricsQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    // ForecastSeeder.Now is Tuesday 22 September 2026, inside the current iteration.
    private static readonly LocalDate PastStart = new(2026, 9, 7);
    private static readonly LocalDate PastEnd = new(2026, 9, 20);
    private static readonly LocalDate CurrentStart = new(2026, 9, 21);
    private static readonly LocalDate CurrentEnd = new(2026, 10, 4);
    private static readonly LocalDate ModelStart = new(2026, 1, 1);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    private static TeamSchedulePeriodDto Kanban(string timeZone = "UTC", SizingMethod sizingMethod = SizingMethod.StoryPoints) =>
        new(ModelStart, null, timeZone, 1, sizingMethod) { Methodology = Methodology.Kanban };

    private static TeamSchedulePeriodDto Scrum() =>
        new(ModelStart, null, "UTC", 1, SizingMethod.StoryPoints) { Methodology = Methodology.Scrum };

    private GetKanbanTeamsWindowMetricsQueryHandler Handler(Dictionary<Guid, TeamSchedulePeriodDto> schedules) =>
        HandlerWithModels(schedules.ToDictionary(s => s.Key, s => new[] { s.Value }));

    private GetKanbanTeamsWindowMetricsQueryHandler HandlerWithModels(Dictionary<Guid, TeamSchedulePeriodDto[]> schedules)
    {
        var dispatcher = new Mock<IDispatcher>();
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsScheduleHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(schedules.ToDictionary(s => s.Key, s => (IReadOnlyList<TeamSchedulePeriodDto>)s.Value));

        return new GetKanbanTeamsWindowMetricsQueryHandler(_fixture.CreateContext(), dispatcher.Object,
            Mock.Of<IDateTimeProvider>(p => p.Now == ForecastSeeder.Now));
    }

    [Fact]
    public async Task Handle_CountsRequirementWorkDoneWithinTheDatesInTheTeamsZone()
    {
        // Arrange — the window runs from 05:00 UTC on 7 September to 05:00 UTC on 21 September in Chicago
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeder = new ForecastSeeder(_fixture);
        await seeder.Reset(cancellationToken);
        var (teamId, _) = await seeder.AddTeam(cancellationToken);

        await seeder.AddItem(teamId, cancellationToken, StatusCategory.Done, done: Instant.FromUtc(2026, 9, 8, 15, 0), storyPoints: 3, activated: Instant.FromUtc(2026, 9, 6, 15, 0));
        await seeder.AddItem(teamId, cancellationToken, StatusCategory.Done, done: Instant.FromUtc(2026, 9, 21, 4, 0), storyPoints: 5);
        await seeder.AddItem(teamId, cancellationToken, StatusCategory.Done, done: Instant.FromUtc(2026, 9, 7, 4, 0), storyPoints: 8);
        await seeder.AddItem(teamId, cancellationToken, StatusCategory.Done, done: Instant.FromUtc(2026, 9, 10, 9, 0));
        await seeder.AddItem(teamId, cancellationToken, StatusCategory.Removed, done: Instant.FromUtc(2026, 9, 10, 9, 0), storyPoints: 13);
        await seeder.AddItem(teamId, cancellationToken, StatusCategory.Done, done: Instant.FromUtc(2026, 9, 10, 9, 0), storyPoints: 21, epic: true);

        var handler = Handler(new() { [teamId] = Kanban("America/Chicago") });

        // Act
        var result = await handler.Handle(new GetKanbanTeamsWindowMetricsQuery([teamId], PastStart, PastEnd), cancellationToken);

        // Assert
        var team = result.Should().ContainSingle().Subject;
        team.Team.Id.Should().Be(teamId);
        team.From.Should().Be(Instant.FromUtc(2026, 9, 7, 5, 0));
        team.Until.Should().Be(Instant.FromUtc(2026, 9, 21, 5, 0));
        team.TimeZone.Should().Be("America/Chicago");
        team.CompletedWorkItems.Should().Be(3);
        team.CompletedEstimate.Should().Be(8);
        team.UnestimatedWorkItems.Should().Be(1);
        team.CycleTime.WorkItemsCount.Should().Be(1);
        team.CycleTime.TotalCycleTimeDays.Should().Be(2);
    }

    [Fact]
    public async Task Handle_LeavesOutScrumTeamsAndTeamsOfTeams()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeder = new ForecastSeeder(_fixture);
        await seeder.Reset(cancellationToken);
        var (kanbanId, _) = await seeder.AddTeam(cancellationToken);
        var (scrumId, _) = await seeder.AddTeam(cancellationToken);
        var (teamOfTeamsId, _) = await seeder.AddTeam(cancellationToken, TeamType.TeamOfTeams);

        var handler = Handler(new() { [kanbanId] = Kanban(), [scrumId] = Scrum(), [teamOfTeamsId] = Kanban() });

        // Act
        var result = await handler.Handle(new GetKanbanTeamsWindowMetricsQuery([kanbanId, scrumId, teamOfTeamsId], PastStart, PastEnd), cancellationToken);

        // Assert
        result.Select(r => r.Team.Id).Should().Equal(kanbanId);
    }

    [Fact]
    public async Task Handle_PastWindow_ReadsInProgressAtItsEndFromHistory()
    {
        // Arrange — the window ends at 00:00 UTC on 21 September
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeder = new ForecastSeeder(_fixture);
        await seeder.Reset(cancellationToken);
        await seeder.MarkHistoryReadToEnd(cancellationToken);
        var (teamId, _) = await seeder.AddTeam(cancellationToken);

        var (activeThen, _) = await seeder.AddItem(teamId, cancellationToken, StatusCategory.Done, done: Instant.FromUtc(2026, 9, 22, 10, 0));
        await seeder.AddPeriod(activeThen, 1, Instant.FromUtc(2026, 9, 15, 0, 0), Instant.FromUtc(2026, 9, 22, 10, 0), StatusCategory.Active, cancellationToken, storyPoints: 5);
        await seeder.AddPeriod(activeThen, 2, Instant.FromUtc(2026, 9, 22, 10, 0), null, StatusCategory.Done, cancellationToken, storyPoints: 5);

        var (startedAfter, _) = await seeder.AddItem(teamId, cancellationToken, StatusCategory.Active);
        await seeder.AddPeriod(startedAfter, 1, Instant.FromUtc(2026, 9, 1, 0, 0), Instant.FromUtc(2026, 9, 21, 6, 0), StatusCategory.Proposed, cancellationToken);
        await seeder.AddPeriod(startedAfter, 2, Instant.FromUtc(2026, 9, 21, 6, 0), null, StatusCategory.Active, cancellationToken);

        var (doneBefore, _) = await seeder.AddItem(teamId, cancellationToken, StatusCategory.Done, done: Instant.FromUtc(2026, 9, 12, 0, 0));
        await seeder.AddPeriod(doneBefore, 1, Instant.FromUtc(2026, 9, 10, 0, 0), Instant.FromUtc(2026, 9, 12, 0, 0), StatusCategory.Active, cancellationToken);
        await seeder.AddPeriod(doneBefore, 2, Instant.FromUtc(2026, 9, 12, 0, 0), null, StatusCategory.Done, cancellationToken);

        var handler = Handler(new() { [teamId] = Kanban() });

        // Act
        var result = await handler.Handle(new GetKanbanTeamsWindowMetricsQuery([teamId], PastStart, PastEnd), cancellationToken);

        // Assert
        var team = result.Should().ContainSingle().Subject;
        team.InProgressWorkItems.Should().Be(1);
        team.InProgressEstimate.Should().Be(5);
        team.InProgressAsOf.Should().Be(Instant.FromUtc(2026, 9, 21, 0, 0));
        team.IsHistoryIncomplete.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_PastWindow_FlagsAWorkspaceWhoseHistoryIsNotReadThrough()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeder = new ForecastSeeder(_fixture);
        await seeder.Reset(cancellationToken);
        var (teamId, _) = await seeder.AddTeam(cancellationToken);
        await seeder.AddItem(teamId, cancellationToken, StatusCategory.Active);

        var handler = Handler(new() { [teamId] = Kanban() });

        // Act
        var result = await handler.Handle(new GetKanbanTeamsWindowMetricsQuery([teamId], PastStart, PastEnd), cancellationToken);

        // Assert
        var team = result.Should().ContainSingle().Subject;
        team.IsHistoryIncomplete.Should().BeTrue();
        team.InProgressWorkItems.Should().Be(0);
    }

    [Fact]
    public async Task Handle_CurrentWindow_ReadsInProgressNow_AndNeedsNoHistory()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeder = new ForecastSeeder(_fixture);
        await seeder.Reset(cancellationToken);
        var (teamId, _) = await seeder.AddTeam(cancellationToken);
        await seeder.AddItem(teamId, cancellationToken, StatusCategory.Active, storyPoints: 2);
        await seeder.AddItem(teamId, cancellationToken, StatusCategory.Active, epic: true);
        await seeder.AddItem(teamId, cancellationToken, StatusCategory.Proposed);

        var handler = Handler(new() { [teamId] = Kanban() });

        // Act
        var result = await handler.Handle(new GetKanbanTeamsWindowMetricsQuery([teamId], CurrentStart, CurrentEnd), cancellationToken);

        // Assert
        var team = result.Should().ContainSingle().Subject;
        team.InProgressWorkItems.Should().Be(1);
        team.InProgressEstimate.Should().Be(2);
        team.InProgressAsOf.Should().Be(ForecastSeeder.Now);
        team.IsHistoryIncomplete.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DoneExactlyAtTheWindowsEnd_CountsInTheNextWindow()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeder = new ForecastSeeder(_fixture);
        await seeder.Reset(cancellationToken);
        var (teamId, _) = await seeder.AddTeam(cancellationToken);
        await seeder.AddItem(teamId, cancellationToken, StatusCategory.Done, done: Instant.FromUtc(2026, 9, 21, 0, 0));
        await seeder.AddItem(teamId, cancellationToken, StatusCategory.Done, done: Instant.FromUtc(2026, 9, 7, 0, 0));

        var handler = Handler(new() { [teamId] = Kanban() });

        // Act
        var result = await handler.Handle(new GetKanbanTeamsWindowMetricsQuery([teamId], PastStart, PastEnd), cancellationToken);

        // Assert
        result.Should().ContainSingle().Which.CompletedWorkItems.Should().Be(1);
    }

    [Fact]
    public async Task Handle_PastWindow_ReadsTheStateJustBeforeItsEnd()
    {
        // Arrange — the window ends at 00:00 UTC on 21 September
        var cancellationToken = TestContext.Current.CancellationToken;
        var end = Instant.FromUtc(2026, 9, 21, 0, 0);
        var seeder = new ForecastSeeder(_fixture);
        await seeder.Reset(cancellationToken);
        await seeder.MarkHistoryReadToEnd(cancellationToken);
        var (teamId, _) = await seeder.AddTeam(cancellationToken);

        // In progress until exactly the end: still in progress at it.
        var (endsAtEnd, _) = await seeder.AddItem(teamId, cancellationToken, StatusCategory.Done, done: end);
        await seeder.AddPeriod(endsAtEnd, 1, Instant.FromUtc(2026, 9, 15, 0, 0), end, StatusCategory.Active, cancellationToken);
        await seeder.AddPeriod(endsAtEnd, 2, end, null, StatusCategory.Done, cancellationToken);

        // Started exactly at the end, through a zero-length period: not yet in progress.
        var (startsAtEnd, _) = await seeder.AddItem(teamId, cancellationToken, StatusCategory.Active);
        await seeder.AddPeriod(startsAtEnd, 1, Instant.FromUtc(2026, 9, 1, 0, 0), end, StatusCategory.Proposed, cancellationToken);
        await seeder.AddPeriod(startsAtEnd, 2, end, end, StatusCategory.Active, cancellationToken);
        await seeder.AddPeriod(startsAtEnd, 3, end, null, StatusCategory.Active, cancellationToken);

        // Removed during the window: not in progress.
        var (removed, _) = await seeder.AddItem(teamId, cancellationToken, StatusCategory.Removed);
        await seeder.AddPeriod(removed, 1, Instant.FromUtc(2026, 9, 8, 0, 0), Instant.FromUtc(2026, 9, 10, 0, 0), StatusCategory.Active, cancellationToken);
        await seeder.AddPeriod(removed, 2, Instant.FromUtc(2026, 9, 10, 0, 0), null, StatusCategory.Removed, cancellationToken);

        var handler = Handler(new() { [teamId] = Kanban() });

        // Act
        var result = await handler.Handle(new GetKanbanTeamsWindowMetricsQuery([teamId], PastStart, PastEnd), cancellationToken);

        // Assert
        result.Should().ContainSingle().Which.InProgressWorkItems.Should().Be(1);
    }

    [Fact]
    public async Task Handle_CountsWorkForTheTeamItIsOnNow()
    {
        // Arrange — the item was in progress at the window's end, and has since moved to another team
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeder = new ForecastSeeder(_fixture);
        await seeder.Reset(cancellationToken);
        await seeder.MarkHistoryReadToEnd(cancellationToken);
        var (formerTeamId, _) = await seeder.AddTeam(cancellationToken);
        var (currentTeamId, _) = await seeder.AddTeam(cancellationToken);
        var (moved, _) = await seeder.AddItem(currentTeamId, cancellationToken, StatusCategory.Active);
        await seeder.AddPeriod(moved, 1, Instant.FromUtc(2026, 9, 10, 0, 0), null, StatusCategory.Active, cancellationToken);

        var handler = Handler(new() { [formerTeamId] = Kanban(), [currentTeamId] = Kanban() });

        // Act
        var result = await handler.Handle(new GetKanbanTeamsWindowMetricsQuery([formerTeamId, currentTeamId], PastStart, PastEnd), cancellationToken);

        // Assert
        result.Single(r => r.Team.Id == currentTeamId).InProgressWorkItems.Should().Be(1);
        result.Single(r => r.Team.Id == formerTeamId).InProgressWorkItems.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ModelBeginningInsideTheWindow_IsMeasuredByTheLastDaysModel()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeder = new ForecastSeeder(_fixture);
        await seeder.Reset(cancellationToken);
        var (teamId, _) = await seeder.AddTeam(cancellationToken);
        var lateModel = new TeamSchedulePeriodDto(PastStart.PlusDays(5), null, "America/Chicago", 1, SizingMethod.Effort) { Methodology = Methodology.Kanban };

        var handler = Handler(new() { [teamId] = lateModel });

        // Act
        var result = await handler.Handle(new GetKanbanTeamsWindowMetricsQuery([teamId], PastStart, PastEnd), cancellationToken);

        // Assert
        var team = result.Should().ContainSingle().Subject;
        team.SizingMethod.Should().Be(SizingMethod.Effort);
        team.From.Should().Be(Instant.FromUtc(2026, 9, 7, 5, 0));
        team.TimeZone.Should().Be("America/Chicago");
    }

    [Fact]
    public async Task Handle_MethodologyOnTheFirstDay_DecidesWhetherTheTeamIsKanban()
    {
        // Arrange — one team turns Scrum and the other Kanban partway through the window
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeder = new ForecastSeeder(_fixture);
        await seeder.Reset(cancellationToken);
        var (becomesScrumId, _) = await seeder.AddTeam(cancellationToken);
        var (becomesKanbanId, _) = await seeder.AddTeam(cancellationToken);
        var switchDate = PastStart.PlusDays(7);

        var handler = HandlerWithModels(new Dictionary<Guid, TeamSchedulePeriodDto[]>
        {
            [becomesScrumId] =
            [
                new(ModelStart, switchDate.PlusDays(-1), "UTC", 1, SizingMethod.Count) { Methodology = Methodology.Kanban },
                new(switchDate, null, "UTC", 1, SizingMethod.Count) { Methodology = Methodology.Scrum },
            ],
            [becomesKanbanId] =
            [
                new(ModelStart, switchDate.PlusDays(-1), "UTC", 1, SizingMethod.Count) { Methodology = Methodology.Scrum },
                new(switchDate, null, "UTC", 1, SizingMethod.Count) { Methodology = Methodology.Kanban },
            ],
        });

        // Act
        var result = await handler.Handle(new GetKanbanTeamsWindowMetricsQuery([becomesScrumId, becomesKanbanId], PastStart, PastEnd), cancellationToken);

        // Assert
        result.Select(r => r.Team.Id).Should().Equal(becomesScrumId);
    }

    [Fact]
    public async Task Handle_FutureWindow_HasNothingInProgress()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeder = new ForecastSeeder(_fixture);
        await seeder.Reset(cancellationToken);
        var (teamId, _) = await seeder.AddTeam(cancellationToken);
        await seeder.AddItem(teamId, cancellationToken, StatusCategory.Active, storyPoints: 2);

        var handler = Handler(new() { [teamId] = Kanban() });

        // Act
        var result = await handler.Handle(new GetKanbanTeamsWindowMetricsQuery([teamId], CurrentEnd.PlusDays(1), CurrentEnd.PlusDays(14)), cancellationToken);

        // Assert
        var team = result.Should().ContainSingle().Subject;
        team.InProgressWorkItems.Should().Be(0);
        team.InProgressAsOf.Should().BeNull();
    }
}
