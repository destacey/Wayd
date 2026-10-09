using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.Requests.Planning.Queries;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Common.Domain.Settings;
using Wayd.Tests.Shared;
using Wayd.Work.Application.Iterations.Sprints;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;

namespace Wayd.Work.Application.Tests.Infrastructure;

/// <summary>
/// One Chicago team with two back-to-back sprints, and a caller whose permissions and team membership each
/// test sets. The clock starts on the Friday before sprint 2's planned Monday start.
/// </summary>
public sealed class SprintLifecycleScenario : IDisposable
{
    public static readonly DateTimeZone Chicago = DateTimeZoneProviders.Tzdb["America/Chicago"];
    public static readonly LocalDate Sprint1Start = new(2026, 9, 14);
    public static readonly LocalDate Sprint2Start = new(2026, 9, 28);

    public SprintLifecycleScenario(Instant? sprint1Started = null, Instant? sprint1Completed = null)
    {
        Team = new WorkTeamFaker(TeamType.Team).Generate();
        Sprint1 = NewSprint(Sprint1Start, 1, sprint1Started, sprint1Completed);
        Sprint2 = NewSprint(Sprint2Start, 2);
        DbContext.AddIterations([Sprint1, Sprint2]);

        Clock = new FakeClock(InChicago(Sprint2Start.PlusDays(-3), 15));
        DateTimeProvider = new TestingDateTimeProvider(Clock);

        TeamSchedulePeriodDto[] schedule = [new TeamSchedulePeriodDto(new LocalDate(2026, 1, 1), null, "America/Chicago", 1, SizingMethod.StoryPoints)];
        Dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamScheduleHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(schedule);
        Dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsScheduleHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetTeamsScheduleHistoryQuery q, CancellationToken _) =>
                q.TeamIds.Where(id => id == Team.Id).ToDictionary(id => id, _ => (IReadOnlyList<TeamSchedulePeriodDto>)schedule));
        Dispatcher
            .Setup(d => d.Send(It.IsAny<IsTeamMemberQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IsTeamMemberQuery q, CancellationToken _) => q.TeamId == Team.Id && q.EmployeeId == EmployeeId && IsMember);
        Dispatcher
            .Setup(d => d.Send(It.IsAny<GetSprintIterationCategoriesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetSprintIterationCategoriesQuery q, CancellationToken _) =>
                MappedCategories.Where(m => q.SprintIds.Contains(m.Key)).ToDictionary());

        SchedulingSettings
            .Setup(s => s.Get(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SchedulingSettings());

        CurrentUser.Setup(u => u.GetUserId()).Returns("user-1");
        CurrentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(EmployeeId);
        CurrentPrincipal
            .Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string permission, CancellationToken _) =>
                (permission == SprintAuthorization.UpdatePermission && CanUpdate)
                || (permission == SprintAuthorization.AdministratorPermission && IsAdministrator));
    }

    public FakeWorkDbContext DbContext { get; } = new();
    public Mock<IDispatcher> Dispatcher { get; } = new();
    public Mock<ISettings<SchedulingSettings>> SchedulingSettings { get; } = new();
    public Mock<ICurrentUser> CurrentUser { get; } = new();
    public Mock<ICurrentPrincipal> CurrentPrincipal { get; } = new();
    public FakeClock Clock { get; }
    public TestingDateTimeProvider DateTimeProvider { get; }

    public Guid EmployeeId { get; } = Guid.NewGuid();
    public WorkTeam Team { get; }
    public Iteration Sprint1 { get; }
    public Iteration Sprint2 { get; }

    public bool CanUpdate { get; set; } = true;
    public bool IsMember { get; set; } = true;
    public bool IsAdministrator { get; set; }

    /// <summary>The category of the planning interval iteration each mapped sprint is in, keyed by sprint id.</summary>
    public Dictionary<Guid, IterationCategory> MappedCategories { get; } = [];

    public static Instant InChicago(LocalDate date, int hour) =>
        date.At(new LocalTime(hour, 0)).InZoneLeniently(Chicago).ToInstant();

    private Iteration NewSprint(LocalDate start, int key, Instant? started = null, Instant? completed = null) =>
        new IterationFaker()
            .AsSprint()
            .WithKey(key)
            .WithTeam(Team)
            .WithDateRange(new IterationDateRange(start, start.PlusDays(13)))
            .WithStarted(started)
            .WithCompleted(completed)
            .Generate();

    public void Dispose() => DbContext.Dispose();
}
