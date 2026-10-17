using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkTeams.Dtos;
using Wayd.Work.Domain.Models.CadenceMetrics;

namespace Wayd.Work.Application.WorkTeams.Queries;

/// <summary>
/// Gets a Kanban team's velocity over its reporting cadence. Null when there is no such team; a team of teams
/// has no cadence, so it is not one.
/// </summary>
/// <param name="PeriodWindow">
/// How many ended periods to measure over, from <see cref="SprintMetricsSettings.MinSprintWindow"/> to
/// <see cref="SprintMetricsSettings.MaxSprintWindow"/>; null for the organization's sprint window.
/// </param>
public sealed record GetTeamCadenceMetricsQuery(Guid TeamId, int? PeriodWindow = null) : IQuery<TeamCadenceMetricsDto?>;

public sealed class GetTeamCadenceMetricsQueryValidator : AbstractValidator<GetTeamCadenceMetricsQuery>
{
    public GetTeamCadenceMetricsQueryValidator()
    {
        RuleFor(q => q.PeriodWindow)
            .InclusiveBetween(SprintMetricsSettings.MinSprintWindow, SprintMetricsSettings.MaxSprintWindow)
            .When(q => q.PeriodWindow.HasValue);
    }
}

public sealed class GetTeamCadenceMetricsQueryHandler(
    IWorkDbContext workDbContext,
    IDispatcher dispatcher,
    ISettings<SprintMetricsSettings> sprintMetricsSettings,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTeamCadenceMetricsQuery, TeamCadenceMetricsDto?>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ISettings<SprintMetricsSettings> _sprintMetricsSettings = sprintMetricsSettings;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<TeamCadenceMetricsDto?> Handle(GetTeamCadenceMetricsQuery request, CancellationToken cancellationToken)
    {
        var isTeam = await _workDbContext.WorkTeams
            .AnyAsync(t => t.Id == request.TeamId && t.Type == TeamType.Team, cancellationToken);
        if (!isTeam)
            return null;

        var defaultWindow = (await _sprintMetricsSettings.Get(cancellationToken)).SprintWindow;
        var window = Math.Clamp(request.PeriodWindow ?? defaultWindow, SprintMetricsSettings.MinSprintWindow, SprintMetricsSettings.MaxSprintWindow);
        var now = _dateTimeProvider.Now;

        var schedule = await _dispatcher.Send(new GetTeamScheduleHistoryQuery(request.TeamId), cancellationToken);
        var current = schedule.FirstOrDefault(p => p.IncludesDate(now.InZone(DateTimeZoneProviders.Tzdb[p.TimeZone]).Date));

        var periods = CadencePeriod.Recent(
            schedule
                .Where(p => p.Cadence is not null)
                .Select(p => new CadenceSchedulePeriod(p.Start, p.End, p.TimeZone, p.SizingMethod, p.Cadence!)),
            now,
            window);

        var completions = periods.Count == 0
            ? []
            : await LoadCompletions(request.TeamId, periods.Min(p => p.From), periods.Max(p => p.Until), cancellationToken);

        var metrics = TeamCadenceMetrics.Measure(current?.SizingMethod ?? SizingMethod.Count, periods, completions);

        return TeamCadenceMetricsDto.From(request.TeamId, window, defaultWindow, current?.Cadence?.Weeks, metrics);
    }

    private async Task<List<CompletedWorkItem>> LoadCompletions(Guid teamId, Instant from, Instant until, CancellationToken cancellationToken)
    {
        // Removed items also carry a DoneTimestamp, so the status category is what excludes them.
        return await _workDbContext.WorkItems
            .Where(w => w.TeamId == teamId)
            .Where(w => w.Type.Level!.Tier == WorkTypeTier.Requirement)
            .Where(w => w.StatusCategory == WorkStatusCategory.Done)
            .Where(w => w.DoneTimestamp >= from && w.DoneTimestamp < until)
            .Select(w => new CompletedWorkItem(w.DoneTimestamp!.Value, w.StoryPoints, w.Effort, w.Size))
            .ToListAsync(cancellationToken);
    }
}
