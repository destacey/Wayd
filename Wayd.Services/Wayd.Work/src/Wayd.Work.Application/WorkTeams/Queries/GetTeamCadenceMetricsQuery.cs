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

/// <summary>Holds the period window to the bounds the sprint window has.</summary>
public sealed class GetTeamCadenceMetricsQueryValidator : AbstractValidator<GetTeamCadenceMetricsQuery>
{
    public GetTeamCadenceMetricsQueryValidator()
    {
        RuleFor(q => q.PeriodWindow)
            .InclusiveBetween(SprintMetricsSettings.MinSprintWindow, SprintMetricsSettings.MaxSprintWindow)
            .When(q => q.PeriodWindow.HasValue);
    }
}

/// <summary>
/// Reads the team's operating models from Organization, works out its recent cadence periods, and measures each
/// from the work items it completed in them.
/// </summary>
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

    /// <inheritdoc />
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

        // With no model covering today, the latest model's unit is still the one its periods were measured in.
        var sizingMethod = (current ?? schedule.LastOrDefault())?.SizingMethod ?? SizingMethod.Count;
        var metrics = TeamCadenceMetrics.Measure(sizingMethod, periods, completions);

        return TeamCadenceMetricsDto.From(request.TeamId, window, defaultWindow, current?.Cadence?.Weeks, metrics);
    }

    /// <summary>The team's Requirement-tier items that reached a Done-category status from <paramref name="from"/> until <paramref name="until"/>.</summary>
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
