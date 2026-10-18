using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.PlanningIntervals;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Common.Models;
using Wayd.Planning.Application.PlanningIntervals.EventHandlers;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Tests.Data;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals.EventHandlers;

/// <summary>
/// <see cref="TeamSprintAutoMappingHandler"/> maps a joining Scrum team's sprints, only into gaps, however often
/// the durable event arrives. A team's methodology is read from its schedule and its type from its Planning copy,
/// so each test adds both.
/// </summary>
public sealed class TeamSprintAutoMappingHandlerTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 2, 9, 0);
    private static readonly LocalDate PiStart = new(2026, 1, 5);
    private static readonly LocalDateRange PiDates = new(PiStart, PiStart.PlusDays(27));

    private readonly Guid _teamId = Guid.NewGuid();
    private readonly Guid _employeeId = Guid.NewGuid();
    private readonly FakePlanningDbContext _planningDbContext = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly Dictionary<Guid, IReadOnlyList<TeamSchedulePeriodDto>> _schedules = [];
    private readonly TeamSprintAutoMappingHandler _handler;

    public TeamSprintAutoMappingHandlerTests()
    {
        _dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsScheduleHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetTeamsScheduleHistoryQuery q, CancellationToken _) =>
                _schedules.Where(s => q.TeamIds.Contains(s.Key)).ToDictionary(s => s.Key, s => s.Value));

        var dateTimeProvider = Mock.Of<IDateTimeProvider>(p => p.Now == Now);
        _handler = new TeamSprintAutoMappingHandler(_planningDbContext, _dispatcher.Object, dateTimeProvider, Mock.Of<ILogger<TeamSprintAutoMappingHandler>>());
    }

    public void Dispose() => _planningDbContext.Dispose();

    [Fact]
    public async Task Handle_ScrumTeamAdded_MapsEachSprintToItsIteration()
    {
        // Arrange
        var planningInterval = PlanningIntervalWithTeam();
        RunsScrum(_teamId);
        var (sprint1, sprint2) = AlignedSprints(_teamId);

        // Act
        await _handler.Handle(TeamsAdded(planningInterval, UserActor(), _teamId), TestContext.Current.CancellationToken);

        // Assert
        var (first, second) = Iterations(planningInterval);
        MappingsOf(planningInterval).Should().BeEquivalentTo([(first.Id, sprint1.Id), (second.Id, sprint2.Id)]);
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ScrumTeamAdded_RecordsTheMappingAsAutomationStartedByWhoAddedTheTeam()
    {
        // Arrange
        var planningInterval = PlanningIntervalWithTeam();
        RunsScrum(_teamId);
        AlignedSprints(_teamId);

        // Act
        await _handler.Handle(TeamsAdded(planningInterval, UserActor(), _teamId), TestContext.Current.CancellationToken);

        // Assert
        var mapped = planningInterval.DomainEvents.OfType<PlanningIntervalSprintMappingsChangedEvent>().Should().ContainSingle().Subject;
        mapped.Actor.Should().Be(EventActor.Automation("user-1", _employeeId));
        mapped.Timestamp.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_TeamAddedByImport_RecordsAutomationWithoutTheRowsEmployee()
    {
        // Arrange
        var planningInterval = PlanningIntervalWithTeam();
        RunsScrum(_teamId);
        AlignedSprints(_teamId);

        // Act
        await _handler.Handle(TeamsAdded(planningInterval, EventActor.Import("user-2", Guid.NewGuid()), _teamId), TestContext.Current.CancellationToken);

        // Assert
        planningInterval.DomainEvents.OfType<PlanningIntervalSprintMappingsChangedEvent>().Single()
            .Actor.Should().Be(EventActor.Automation("user-2"));
    }

    [Fact]
    public async Task Handle_TeamAddedBySystem_RecordsTheSystem()
    {
        // Arrange
        var planningInterval = PlanningIntervalWithTeam();
        RunsScrum(_teamId);
        AlignedSprints(_teamId);

        // Act
        await _handler.Handle(TeamsAdded(planningInterval, EventActor.System, _teamId), TestContext.Current.CancellationToken);

        // Assert
        planningInterval.DomainEvents.OfType<PlanningIntervalSprintMappingsChangedEvent>().Single()
            .Actor.Should().Be(EventActor.System);
    }

    [Fact]
    public async Task Handle_KanbanTeamAdded_MapsNothing()
    {
        // Arrange
        var planningInterval = PlanningIntervalWithTeam();
        _schedules[_teamId] = [Period(PiStart.PlusDays(-30), null, Methodology.Kanban)];
        AlignedSprints(_teamId);

        // Act
        await _handler.Handle(TeamsAdded(planningInterval, UserActor(), _teamId), TestContext.Current.CancellationToken);

        // Assert
        planningInterval.IterationSprints.Should().BeEmpty();
        _planningDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_TeamOfTeamsAdded_MapsNothing()
    {
        // Arrange — a team of teams has an operating model too, so its type is what rules it out.
        var planningInterval = PlanningIntervalWithTeam(TeamType.TeamOfTeams);
        RunsScrum(_teamId);
        AlignedSprints(_teamId);

        // Act
        await _handler.Handle(TeamsAdded(planningInterval, UserActor(), _teamId), TestContext.Current.CancellationToken);

        // Assert
        planningInterval.IterationSprints.Should().BeEmpty();
        _planningDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_TeamWithoutAnOperatingModel_MapsNothing()
    {
        // Arrange — missing from the schedules.
        var planningInterval = PlanningIntervalWithTeam();
        AlignedSprints(_teamId);

        // Act
        await _handler.Handle(TeamsAdded(planningInterval, UserActor(), _teamId), TestContext.Current.CancellationToken);

        // Assert
        planningInterval.IterationSprints.Should().BeEmpty();
        _planningDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_TeamRemovedBeforeTheEventArrives_MapsNothing()
    {
        // Arrange
        _planningDbContext.AddPlanningTeam(new PlanningTeamFaker(TeamType.Team).WithId(_teamId).Generate());
        var planningInterval = new PlanningIntervalFaker().WithKey(7).WithIterations(PiDates).Generate();
        _planningDbContext.AddPlanningInterval(planningInterval);
        RunsScrum(_teamId);
        AlignedSprints(_teamId);

        // Act
        await _handler.Handle(TeamsAdded(planningInterval, UserActor(), _teamId), TestContext.Current.CancellationToken);

        // Assert
        planningInterval.IterationSprints.Should().BeEmpty();
        _planningDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_TeamSwitchesToKanbanDuringTheInterval_MapsOnlyItsScrumIterations()
    {
        // Arrange
        var planningInterval = PlanningIntervalWithTeam();
        var (_, second) = Iterations(planningInterval);
        _schedules[_teamId] =
        [
            Period(PiStart.PlusDays(-30), second.DateRange.Start.PlusDays(-1), Methodology.Scrum),
            Period(second.DateRange.Start, null, Methodology.Kanban),
        ];
        var (sprint1, _) = AlignedSprints(_teamId);

        // Act
        await _handler.Handle(TeamsAdded(planningInterval, UserActor(), _teamId), TestContext.Current.CancellationToken);

        // Assert
        var (first, _) = Iterations(planningInterval);
        MappingsOf(planningInterval).Should().BeEquivalentTo([(first.Id, sprint1.Id)]);
    }

    [Fact]
    public async Task Handle_TeamAlreadyHasAMapping_KeepsItAndFillsOnlyTheOtherIterations()
    {
        // Arrange — the second sprint was mapped to the first iteration by hand.
        var planningInterval = PlanningIntervalWithTeam();
        RunsScrum(_teamId);
        var (_, sprint2) = AlignedSprints(_teamId);
        var (first, second) = Iterations(planningInterval);
        MapByHand(planningInterval, first, sprint2);

        var sprint3 = AddSprint(_teamId, second.DateRange.Start.PlusDays(1), second.DateRange.End);

        // Act
        await _handler.Handle(TeamsAdded(planningInterval, UserActor(), _teamId), TestContext.Current.CancellationToken);

        // Assert — sprint 2 stays where it was put, so the second iteration takes the next best match.
        MappingsOf(planningInterval).Should().BeEquivalentTo([(first.Id, sprint2.Id), (second.Id, sprint3.Id)]);
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Redelivered_ChangesNothingTheSecondTime()
    {
        // Arrange
        var planningInterval = PlanningIntervalWithTeam();
        RunsScrum(_teamId);
        AlignedSprints(_teamId);
        var @event = TeamsAdded(planningInterval, UserActor(), _teamId);
        await _handler.Handle(@event, TestContext.Current.CancellationToken);
        LinkMappedSprints(planningInterval);
        var mappingsAfterFirst = MappingsOf(planningInterval);

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        MappingsOf(planningInterval).Should().BeEquivalentTo(mappingsAfterFirst);
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_TeamWithAMappedSprintThatIsNoLongerASprint_SkipsItAndMapsTheOtherTeams()
    {
        // Arrange — the first team's mapped sprint became an iteration when the team moved to Kanban.
        var otherTeamId = Guid.NewGuid();
        _planningDbContext.AddPlanningTeam(new PlanningTeamFaker(TeamType.Team).WithId(_teamId).Generate());
        _planningDbContext.AddPlanningTeam(new PlanningTeamFaker(TeamType.Team).WithId(otherTeamId).Generate());
        var planningInterval = new PlanningIntervalFaker().WithKey(7).WithTeams(_teamId, otherTeamId).WithIterations(PiDates).Generate();
        _planningDbContext.AddPlanningInterval(planningInterval);
        RunsScrum(_teamId);
        RunsScrum(otherTeamId);

        var (first, second) = Iterations(planningInterval);
        var (turnedIntoIteration, _) = AlignedSprints(_teamId);
        MapByHand(planningInterval, first, turnedIntoIteration);
        turnedIntoIteration.ApplyDetails(turnedIntoIteration.Name, IterationType.Iteration, Now).Should().BeTrue();
        var (otherSprint1, otherSprint2) = AlignedSprints(otherTeamId);

        // Act
        await _handler.Handle(TeamsAdded(planningInterval, UserActor(), _teamId, otherTeamId), TestContext.Current.CancellationToken);

        // Assert
        MappingsOf(planningInterval).Should().BeEquivalentTo([
            (first.Id, turnedIntoIteration.Id),
            (first.Id, otherSprint1.Id),
            (second.Id, otherSprint2.Id),
        ]);
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_PlanningIntervalDeleted_DoesNothing()
    {
        // Arrange
        RunsScrum(_teamId);
        AlignedSprints(_teamId);
        var deleted = new PlanningIntervalFaker().WithKey(7).WithTeams(_teamId).WithIterations(PiDates).Generate();

        // Act
        await _handler.Handle(TeamsAdded(deleted, UserActor(), _teamId), TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_OnlyTeamsRemoved_DoesNothing()
    {
        // Arrange
        var planningInterval = PlanningIntervalWithTeam();

        // Act
        await _handler.Handle(new PlanningIntervalTeamsChangedEvent(planningInterval.Id, planningInterval.Key, [], [Guid.NewGuid()], [_teamId], UserActor(), Now), TestContext.Current.CancellationToken);

        // Assert
        _dispatcher.Verify(d => d.Send(It.IsAny<GetTeamsScheduleHistoryQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        _planningDbContext.SaveChangesCallCount.Should().Be(0);
    }

    private EventActor UserActor() => EventActor.User("user-1", _employeeId);

    private PlanningInterval PlanningIntervalWithTeam(TeamType teamType = TeamType.Team)
    {
        _planningDbContext.AddPlanningTeam(new PlanningTeamFaker(teamType).WithId(_teamId).Generate());

        var planningInterval = new PlanningIntervalFaker()
            .WithKey(7)
            .WithTeams(_teamId)
            .WithIterations(PiDates)
            .Generate();
        _planningDbContext.AddPlanningInterval(planningInterval);
        return planningInterval;
    }

    private static (PlanningIntervalIteration First, PlanningIntervalIteration Second) Iterations(PlanningInterval planningInterval)
    {
        var ordered = planningInterval.Iterations.OrderBy(i => i.DateRange.Start).ToList();
        return (ordered[0], ordered[1]);
    }

    private void RunsScrum(Guid teamId) =>
        _schedules[teamId] = [Period(PiStart.PlusDays(-30), null, Methodology.Scrum)];

    private static TeamSchedulePeriodDto Period(LocalDate start, LocalDate? end, Methodology methodology) =>
        new(start, end, "UTC", 1, SizingMethod.StoryPoints) { Methodology = methodology };

    private (PlanningSprint, PlanningSprint) AlignedSprints(Guid teamId) =>
        (AddSprint(teamId, PiStart, PiStart.PlusDays(13)), AddSprint(teamId, PiStart.PlusDays(14), PiStart.PlusDays(27)));

    private PlanningSprint AddSprint(Guid teamId, LocalDate start, LocalDate end)
    {
        var sprint = new PlanningSprintFaker()
            .AsSprint()
            .WithTeamId(teamId)
            .WithDateRange(IterationDateRange.Create(start, end))
            .Generate();
        _planningDbContext.AddPlanningSprint(sprint);
        return sprint;
    }

    private void MapByHand(PlanningInterval planningInterval, PlanningIntervalIteration iteration, PlanningSprint sprint)
    {
        planningInterval.MapSprintToIteration(iteration.Id, sprint, EventActor.User("user-9"), Now).IsSuccess.Should().BeTrue();
        LinkMappedSprints(planningInterval);
        planningInterval.ClearDomainEvents();
    }

    // EF loads each mapping's Sprint navigation, which the handler reads to find the team's mappings; the fake
    // does not, so a test that saves and handles again links them itself.
    private void LinkMappedSprints(PlanningInterval planningInterval)
    {
        foreach (var mapping in planningInterval.IterationSprints.Where(m => m.Sprint is null))
            mapping.SetPrivate(m => m.Sprint, _planningDbContext.PlanningSprints.Single(s => s.Id == mapping.SprintId));
    }

    private static List<(Guid IterationId, Guid SprintId)> MappingsOf(PlanningInterval planningInterval) =>
        [.. planningInterval.IterationSprints.Select(m => (m.PlanningIntervalIterationId, m.SprintId))];

    private static PlanningIntervalTeamsChangedEvent TeamsAdded(PlanningInterval planningInterval, EventActor actor, params Guid[] teamIds) =>
        new(planningInterval.Id, planningInterval.Key, teamIds, [], [.. planningInterval.Teams.Select(t => t.TeamId)], actor, Now);
}
