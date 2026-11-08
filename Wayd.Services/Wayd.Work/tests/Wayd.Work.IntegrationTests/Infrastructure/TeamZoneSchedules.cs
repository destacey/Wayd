using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Settings;

namespace Wayd.Work.IntegrationTests.Infrastructure;

/// <summary>
/// A dispatcher that answers each team's schedule history with one open-ended period in the given zone, and
/// default scheduling and general settings, for handlers that read sprint days in their team's zone.
/// </summary>
public sealed class TeamZoneSchedules
{
    public TeamZoneSchedules(params (Guid TeamId, string TimeZone)[] teams)
    {
        var periods = teams.ToDictionary(
            t => t.TeamId,
            t => (IReadOnlyList<TeamSchedulePeriodDto>)[new TeamSchedulePeriodDto(new LocalDate(2026, 1, 1), null, t.TimeZone, 1, SizingMethod.StoryPoints)]);

        Dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsScheduleHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetTeamsScheduleHistoryQuery q, CancellationToken _) =>
                q.TeamIds.Where(periods.ContainsKey).ToDictionary(id => id, id => periods[id]));
        SchedulingSettings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSettings());
        GeneralSettings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new GeneralSettings());
    }

    public Mock<IDispatcher> Dispatcher { get; } = new();
    public Mock<ISettings<SchedulingSettings>> SchedulingSettings { get; } = new();
    public Mock<ISettings<GeneralSettings>> GeneralSettings { get; } = new();
}
