using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.Requests.Planning.Queries;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.Models;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Iterations.Dtos;
using Wayd.Work.Application.Iterations.Queries;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Models.SprintMetrics;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because each sprint's history is read with a work type tier lookup inside the
/// projection and a workspace navigation in the completeness check, which only a real provider translates.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetTeamSprintMetricsQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private const string SystemId = "team-sprint-metrics-test";

    // Sprint 1 starts Monday 1 June 2026; each sprint is two weeks, back to back, counted in UTC with a
    // one-day grace period and sized in story points.
    private static readonly LocalDate FirstStart = new(2026, 6, 1);
    private static readonly Instant AfterEverySprint = Instant.FromUtc(2026, 9, 1, 12, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_FillsTheWindowPastExcludedSprints_AndListsThemWithTheirReason()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var workspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "TSM", ct);
        await WorkItemHistorySeeder.MarkHistoryReadToEnd(_fixture, workspaceId, ct);
        var teamId = await SeedTeam(ct);

        var sprint1 = await SeedSprint(teamId, 1, ct);
        var sprint2 = await SeedSprint(teamId, 2, ct);
        var sprint3 = await SeedSprint(teamId, 3, ct);
        var sprint4 = await SeedSprint(teamId, 4, ct);
        var sprint5 = await SeedSprint(teamId, 5, ct);

        await SeedCommitted(workspaceId, sprint1, 1, externalId: 11, done: true, ct);
        await SeedCommitted(workspaceId, sprint1, 1, externalId: 12, done: false, ct);
        await SeedCommitted(workspaceId, sprint2, 2, externalId: 21, done: true, ct);
        await SeedCommitted(workspaceId, sprint2, 2, externalId: 22, done: false, ct);
        // Sprint 3 has no work, so nothing was committed to it.
        await SeedCommitted(workspaceId, sprint4, 4, externalId: 41, done: true, ct);
        await SeedCommitted(workspaceId, sprint4, 4, externalId: 42, done: true, ct);
        // Sprint 5 has work, but is mapped to an innovation and planning iteration.
        await SeedCommitted(workspaceId, sprint5, 5, externalId: 51, done: true, ct);

        // Act
        var result = await Handle(teamId, sprintWindow: 3, ct, nonStandard: sprint5);

        // Assert
        result.SprintWindow.Should().Be(3);
        result.DefaultSprintWindow.Should().Be(7);
        result.SizingMethod.Should().Be(SizingMethod.StoryPoints);

        result.Sprints.Select(s => s.Id).Should().Equal(sprint4, sprint2, sprint1);
        result.Sprints.Should().OnlyContain(s => s.InEstimateAverages);
        result.Sprints[0].ByCount.Should().Be(new SprintMetricFiguresDto(2, 1, 1));
        result.Sprints[0].ByEstimate.Should().Be(new SprintMetricFiguresDto(10, 1, 1));
        result.Sprints[1].ByCount.Should().Be(new SprintMetricFiguresDto(1, 0.5, 0.5));

        result.Excluded.Select(e => (e.Id, e.Reason)).Should().Equal(
            (sprint5, SprintMetricsExclusion.NonStandard),
            (sprint3, SprintMetricsExclusion.NoCommitment));

        result.ByCount.Velocity!.Average.Should().BeApproximately(4.0 / 3, 1e-9);
        result.ByCount.Velocity.Min.Should().Be(1);
        result.ByCount.Velocity.Max.Should().Be(2);
        result.ByCount.Velocity.Sprints.Should().Be(3);
        result.ByCount.SayDo!.Average.Should().BeApproximately(2.0 / 3, 1e-9);
        result.ByCount.Predictability!.Average.Should().BeApproximately(2.0 / 3, 1e-9);
        result.ByEstimate.Velocity!.Average.Should().BeApproximately(20.0 / 3, 1e-9);
    }

    [Fact]
    public async Task Handle_SprintWhoseHistoryIsIncomplete_IsExcluded()
    {
        // Arrange — the workspace's history has never been read through to the end
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var workspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "TSM", ct);
        var teamId = await SeedTeam(ct);
        var sprint1 = await SeedSprint(teamId, 1, ct);
        await SeedCommitted(workspaceId, sprint1, 1, externalId: 11, done: true, ct);

        // Act
        var result = await Handle(teamId, sprintWindow: null, ct);

        // Assert
        result.SprintWindow.Should().Be(7);
        result.Sprints.Should().BeEmpty();
        result.Excluded.Should().ContainSingle().Which.Reason.Should().Be(SprintMetricsExclusion.HistoryIncomplete);
        result.ByCount.Velocity.Should().BeNull();
    }

    [Fact]
    public async Task Handle_TeamWithNoSprints_IsAnEmptyReport()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var teamId = await SeedTeam(ct);

        // Act
        var result = await Handle(teamId, sprintWindow: null, ct);

        // Assert
        result.Sprints.Should().BeEmpty();
        result.Excluded.Should().BeEmpty();
        result.SizingMethod.Should().Be(SizingMethod.Count);
    }

    private static LocalDate StartOf(int number) => FirstStart.PlusDays((number - 1) * 14);

    private async Task<TeamSprintMetricsDto> Handle(Guid teamId, int? sprintWindow, CancellationToken ct, Guid? nonStandard = null)
    {
        await using var accessor = new WaydDbContextAccessor(_fixture);

        var dispatcher = new Mock<IDispatcher>();
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamScheduleHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TeamSchedulePeriodDto(new LocalDate(2026, 1, 1), null, "UTC", 1, SizingMethod.StoryPoints)]);
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetSprintIterationCategoriesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(nonStandard is { } sprintId
                ? new Dictionary<Guid, IterationCategory> { [sprintId] = IterationCategory.InnovationAndPlanning }
                : []);

        var schedulingSettings = new Mock<ISettings<SchedulingSettings>>();
        schedulingSettings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSettings());
        var sprintMetricsSettings = new Mock<ISettings<SprintMetricsSettings>>();
        sprintMetricsSettings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new SprintMetricsSettings());

        var handler = new GetTeamSprintMetricsQueryHandler(accessor.Context, dispatcher.Object, schedulingSettings.Object,
            sprintMetricsSettings.Object, Mock.Of<IDateTimeProvider>(p => p.Now == AfterEverySprint));
        return await handler.Handle(new GetTeamSprintMetricsQuery(teamId, sprintWindow), ct);
    }

    private async Task<Guid> SeedTeam(CancellationToken ct)
    {
        var key = Random.Shared.Next(100_000, 999_999);
        var team = new WorkTeam(new SourceTeam(Guid.NewGuid(), key, "Atlas", new TeamCode($"T{key}"), TeamType.Team, true), SqlServerDbContextFixture.FixedNow);

        await using var accessor = new WaydDbContextAccessor(_fixture);
        accessor.Context.WorkTeams.Add(team);
        await accessor.Context.SaveChangesAsync(ct);

        return team.Id;
    }

    private async Task<Guid> SeedSprint(Guid teamId, int number, CancellationToken ct)
    {
        var sprint = Iteration.Create($"Sprint {number}", IterationType.Sprint,
            new IterationDateRange(StartOf(number), StartOf(number).PlusDays(13)), teamId,
            OwnershipInfo.CreateWaydOwned(), [], EventActor.System, SqlServerDbContextFixture.FixedNow);

        await using var accessor = new WaydDbContextAccessor(_fixture);
        accessor.Context.Iterations.Add(sprint);
        await accessor.Context.SaveChangesAsync(ct);

        return sprint.Id;
    }

    /// <summary>A five-point item in the sprint from before its commitment point, finished mid-sprint or left unfinished in it.</summary>
    private async Task SeedCommitted(Guid workspaceId, Guid sprintId, int sprintNumber, int externalId, bool done, CancellationToken ct)
    {
        Instant Day(int offset) => StartOf(sprintNumber).PlusDays(offset).At(new LocalTime(12, 0)).InUtc().ToInstant();

        var workItemId = await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, externalId, ct);
        if (done)
        {
            await WorkItemHistorySeeder.SeedPeriod(_fixture, workItemId, workspaceId, 1, Day(-2), Day(5), sprintId, "Active", ct, storyPoints: 5);
            await WorkItemHistorySeeder.SeedPeriod(_fixture, workItemId, workspaceId, 2, Day(5), null, sprintId, "Done", ct, storyPoints: 5);
        }
        else
        {
            await WorkItemHistorySeeder.SeedPeriod(_fixture, workItemId, workspaceId, 1, Day(-2), null, sprintId, "Active", ct, storyPoints: 5);
        }
    }

    private sealed record SourceTeam(Guid Id, int Key, string Name, TeamCode Code, TeamType Type, bool IsActive) : ISimpleTeam;
}
