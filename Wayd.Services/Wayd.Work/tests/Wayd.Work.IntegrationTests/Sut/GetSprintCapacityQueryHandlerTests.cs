using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Requests.Organization;
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
using Wayd.Work.Domain.Models.SprintCapacity;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// A sprint's capacity beside its scope against a real SQL Server container.
/// </summary>
/// <remarks>
/// The scope is read through the work item history and work type tier lookups sprint scope uses, which only a
/// real provider translates. Organization's capacity and the team's sprint metrics are answered by the dispatcher.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetSprintCapacityQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private const string SystemId = "sprint-capacity-test";

    // A team with no operating model takes the system defaults: UTC, a one-day grace period, by item count.
    // Committed at the end of Monday 14 September.
    private static readonly LocalDate SprintStart = new(2026, 9, 14);
    private static readonly LocalDate SprintEnd = new(2026, 9, 25);
    private static readonly Instant DuringTheSprint = Instant.FromUtc(2026, 9, 20, 12, 0);
    private static readonly Instant BeforeTheSprint = Instant.FromUtc(2026, 9, 10, 12, 0);
    private static readonly Instant AfterTheSprint = Instant.FromUtc(2026, 10, 15, 12, 0);

    // Two completed sprints that each delivered 2 items with 10 person-days of delivery capacity: 0.2 a person-day.
    private static readonly TeamSprintMetricsSprintDto[] PriorSprints =
    [
        Prior(new LocalDate(2026, 8, 31), new LocalDate(2026, 9, 11), 2),
        Prior(new LocalDate(2026, 8, 17), new LocalDate(2026, 8, 28), 2),
    ];

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_WhenTheCommittedScopeIsMoreThanTheCapacityIsExpectedToDeliver_WarnsOverCapacity()
    {
        // Arrange — three items committed against 10 person-days, which the team's recent rate puts at 2 items
        var ct = TestContext.Current.CancellationToken;
        var (teamId, sprintId) = await SeedCommittedSprint(3, ct);
        var dispatcher = Dispatcher(teamId, sprintDelivery: 10m, priorDelivery: 10m);

        // Act
        var capacity = await Handle(sprintId, DuringTheSprint, dispatcher.Object, ct);

        // Assert
        capacity.Should().NotBeNull();
        capacity!.ScopeBasis.Should().Be(SprintCapacityScopeBasis.Committed);
        capacity.Scope.Should().Be(3);
        capacity.SizingMethod.Should().Be(SizingMethod.Count);
        capacity.DeliveryPersonDays.Should().Be(10m);
        capacity.HasAllocations.Should().BeTrue();
        capacity.Outcome.Should().Be(SprintCapacityOutcome.Compared);
        capacity.ExpectedScope.Should().BeApproximately(2, 1e-9);
        capacity.PriorSprints.Should().Be(2);
        capacity.IsOverCapacity.Should().BeTrue();
        dispatcher.Verify(d => d.Send(
            It.Is<GetTeamsCapacityByPeriodQuery>(q =>
                q.TeamIds.SequenceEqual(new[] { teamId })
                && q.Periods.SequenceEqual(new[]
                {
                    new CapacityPeriod(SprintStart, SprintEnd),
                    new CapacityPeriod(PriorSprints[0].Start, PriorSprints[0].End),
                    new CapacityPeriod(PriorSprints[1].Start, PriorSprints[1].End),
                })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTheCapacityCoversTheCommittedScope_DoesNotWarn()
    {
        // Arrange — the same three items, with twice the capacity: 4 items expected
        var ct = TestContext.Current.CancellationToken;
        var (teamId, sprintId) = await SeedCommittedSprint(3, ct);
        var dispatcher = Dispatcher(teamId, sprintDelivery: 20m, priorDelivery: 10m);

        // Act
        var capacity = await Handle(sprintId, DuringTheSprint, dispatcher.Object, ct);

        // Assert
        capacity!.ExpectedScope.Should().BeApproximately(4, 1e-9);
        capacity.IsOverCapacity.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_BeforeTheCommitmentPoint_SetsTheWorkInTheSprintNowAgainstTheCapacity()
    {
        // Arrange — three items in the sprint now, one of them added after the others
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var teamId = await SeedTeam(ct);
        var sprintId = await SeedTeamSprint(teamId, ct);
        var workspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "CAPA", ct);
        for (var i = 1; i <= 3; i++)
            await PutInSprint(await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, i, ct), sprintId, ct);
        await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, 4, ct);
        var dispatcher = Dispatcher(teamId, sprintDelivery: 10m, priorDelivery: 10m);

        // Act
        var capacity = await Handle(sprintId, BeforeTheSprint, dispatcher.Object, ct);

        // Assert
        capacity!.ScopeBasis.Should().Be(SprintCapacityScopeBasis.Planned);
        capacity.Scope.Should().Be(3);
        capacity.IsOverCapacity.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AsksForTheSprintsTeamDaysOffToBeLeftOut()
    {
        // Arrange — the team is off the sprint's second Monday, and half of the Tuesday after
        var ct = TestContext.Current.CancellationToken;
        var (teamId, sprintId) = await SeedCommittedSprint(3, ct);
        TeamDayOff dayOff = new(SprintStart.PlusDays(7));
        TeamDayOff halfDayOff = new(SprintStart.PlusDays(8), 50);
        await using (var accessor = new WaydDbContextAccessor(_fixture))
        {
            var sprint = await accessor.Context.Iterations.SingleAsync(i => i.Id == sprintId, ct);
            sprint.SetTeamDaysOff([dayOff, halfDayOff], EventActor.System, Instant.FromUtc(2026, 9, 1, 0, 0)).IsSuccess.Should().BeTrue();
            await accessor.Context.SaveChangesAsync(ct);
        }
        var dispatcher = Dispatcher(teamId, sprintDelivery: 10m, priorDelivery: 10m);

        // Act
        await Handle(sprintId, DuringTheSprint, dispatcher.Object, ct);

        // Assert
        dispatcher.Verify(d => d.Send(
            It.Is<GetTeamsCapacityByPeriodQuery>(q => q.Periods[0] == new CapacityPeriod(SprintStart, SprintEnd, new[] { dayOff, halfDayOff })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ForACompletedSprint_ComparesWithoutWarning()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (teamId, sprintId) = await SeedCommittedSprint(3, ct);
        var dispatcher = Dispatcher(teamId, sprintDelivery: 10m, priorDelivery: 10m);

        // Act
        var capacity = await Handle(sprintId, AfterTheSprint, dispatcher.Object, ct);

        // Assert
        capacity!.Outcome.Should().Be(SprintCapacityOutcome.Compared);
        capacity.Scope.Should().Be(3);
        capacity.IsOverCapacity.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithTheFeatureOff_ReturnsNull()
    {
        // Arrange — Organization answers the capacity read with null when the feature is off
        var ct = TestContext.Current.CancellationToken;
        var (teamId, sprintId) = await SeedCommittedSprint(3, ct);
        var dispatcher = Dispatcher(teamId, sprintDelivery: 10m, priorDelivery: 10m);
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsCapacityByPeriodQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<Guid, IReadOnlyList<TeamPeriodCapacity>>?)null);

        // Act
        var capacity = await Handle(sprintId, DuringTheSprint, dispatcher.Object, ct);

        // Assert
        capacity.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ForASprintWithNoTeam_ReturnsNullWithoutReadingCapacity()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var sprintId = await WorkItemHistorySeeder.SeedSprint(_fixture, SprintStart, SprintEnd, ct);
        var dispatcher = Dispatcher(Guid.NewGuid(), sprintDelivery: 10m, priorDelivery: 10m);

        // Act
        var capacity = await Handle(sprintId, DuringTheSprint, dispatcher.Object, ct);

        // Assert
        capacity.Should().BeNull();
        dispatcher.Verify(d => d.Send(It.IsAny<GetTeamsCapacityByPeriodQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A team sprint with <paramref name="committed"/> items in it from before its commitment point.</summary>
    private async Task<(Guid TeamId, Guid SprintId)> SeedCommittedSprint(int committed, CancellationToken ct)
    {
        await _fixture.ResetWorkData(ct);
        var teamId = await SeedTeam(ct);
        var sprintId = await SeedTeamSprint(teamId, ct);
        var workspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "CAPA", ct);
        await WorkItemHistorySeeder.MarkHistoryReadToEnd(_fixture, workspaceId, ct);

        for (var i = 1; i <= committed; i++)
        {
            var item = await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, i, ct);
            await WorkItemHistorySeeder.SeedPeriod(_fixture, item, workspaceId, 1, Instant.FromUtc(2026, 9, 10, 12, 0), null, sprintId, "Active", ct);
        }

        return (teamId, sprintId);
    }

    /// <summary>
    /// A dispatcher for a team with no operating model, whose two prior sprints delivered 2 items each with
    /// <paramref name="priorDelivery"/> person-days, and whose sprint has <paramref name="sprintDelivery"/>.
    /// </summary>
    private static Mock<IDispatcher> Dispatcher(Guid teamId, decimal sprintDelivery, decimal priorDelivery)
    {
        var dispatcher = HolidayDispatcher.With();
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamScheduleHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamSprintMetricsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TeamSprintMetricsDto
            {
                TeamId = teamId,
                SizingMethod = SizingMethod.Count,
                ByCount = new SprintMetricSummariesDto(null, null, null),
                ByEstimate = new SprintMetricSummariesDto(null, null, null),
                Sprints = [.. PriorSprints],
                Excluded = [],
            });
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsCapacityByPeriodQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetTeamsCapacityByPeriodQuery q, CancellationToken _) =>
                new Dictionary<Guid, IReadOnlyList<TeamPeriodCapacity>>
                {
                    [teamId] = [.. q.Periods.Select((p, i) => i == 0
                        ? new TeamPeriodCapacity(p, sprintDelivery, sprintDelivery, 0m, true)
                        : new TeamPeriodCapacity(p, priorDelivery, priorDelivery, 0m, true))],
                });
        return dispatcher;
    }

    private static TeamSprintMetricsSprintDto Prior(LocalDate start, LocalDate end, double velocity) => new()
    {
        Id = Guid.NewGuid(),
        Key = Random.Shared.Next(1_000, 9_999),
        Name = "Prior sprint",
        Start = start,
        End = end,
        SizingMethod = SizingMethod.Count,
        InEstimateAverages = true,
        ByCount = new SprintMetricFiguresDto(velocity, 1, 1),
        ByEstimate = new SprintMetricFiguresDto(velocity, 1, 1),
    };

    private async Task PutInSprint(Guid workItemId, Guid sprintId, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [Work].[WorkItems] SET [IterationId] = {sprintId} WHERE [Id] = {workItemId}", ct);
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

    private async Task<Guid> SeedTeamSprint(Guid teamId, CancellationToken ct)
    {
        var sprint = Iteration.Create("Team Sprint", IterationType.Sprint,
            new IterationDateRange(SprintStart, SprintEnd), teamId,
            OwnershipInfo.CreateWaydOwned(), [], EventActor.System, SqlServerDbContextFixture.FixedNow);

        await using var accessor = new WaydDbContextAccessor(_fixture);
        accessor.Context.Iterations.Add(sprint);
        await accessor.Context.SaveChangesAsync(ct);

        return sprint.Id;
    }

    private async Task<SprintCapacityDto?> Handle(Guid sprintId, Instant now, IDispatcher dispatcher, CancellationToken ct)
    {
        await using var accessor = new WaydDbContextAccessor(_fixture);

        var schedulingSettings = new Mock<ISettings<SchedulingSettings>>();
        schedulingSettings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSettings());

        var handler = new GetSprintCapacityQueryHandler(accessor.Context, dispatcher, schedulingSettings.Object, Mock.Of<IDateTimeProvider>(p => p.Now == now));
        return await handler.Handle(new GetSprintCapacityQuery(new IdOrKey(sprintId.ToString())), ct);
    }

    private sealed record SourceTeam(Guid Id, int Key, string Name, TeamCode Code, TeamType Type, bool IsActive) : ISimpleTeam;
}
