using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Iterations.Dtos;
using Wayd.Work.Application.Iterations.Sprints;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Domain.Models.SprintMetrics;
using Wayd.Work.Domain.Models.SprintScope;

namespace Wayd.Work.Application.Iterations.Queries;

/// <summary>
/// Gets a team's velocity, predictability and say/do over its last completed standard sprints. Null when
/// there is no such team; a team of teams has no sprints of its own, so it is not one.
/// </summary>
/// <param name="SprintWindow">
/// How many sprints to measure over, from <see cref="SprintMetricsSettings.MinSprintWindow"/> to
/// <see cref="SprintMetricsSettings.MaxSprintWindow"/>; null for the organization's sprint window.
/// </param>
public sealed record GetTeamSprintMetricsQuery(Guid TeamId, int? SprintWindow = null) : IQuery<TeamSprintMetricsDto?>;

public sealed class GetTeamSprintMetricsQueryValidator : AbstractValidator<GetTeamSprintMetricsQuery>
{
    public GetTeamSprintMetricsQueryValidator()
    {
        RuleFor(q => q.SprintWindow)
            .InclusiveBetween(SprintMetricsSettings.MinSprintWindow, SprintMetricsSettings.MaxSprintWindow)
            .When(q => q.SprintWindow.HasValue);
    }
}

public sealed class GetTeamSprintMetricsQueryHandler(
    IWorkDbContext workDbContext,
    IDispatcher dispatcher,
    ISettings<SchedulingSettings> schedulingSettings,
    ISettings<SprintMetricsSettings> sprintMetricsSettings,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTeamSprintMetricsQuery, TeamSprintMetricsDto?>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly ISettings<SprintMetricsSettings> _sprintMetricsSettings = sprintMetricsSettings;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<TeamSprintMetricsDto?> Handle(GetTeamSprintMetricsQuery request, CancellationToken cancellationToken)
    {
        var isTeam = await _workDbContext.WorkTeams
            .AnyAsync(t => t.Id == request.TeamId && t.Type == TeamType.Team, cancellationToken);
        if (!isTeam)
            return null;

        var defaultWindow = (await _sprintMetricsSettings.Get(cancellationToken)).SprintWindow;
        var now = _dateTimeProvider.Now;

        var timeline = await _workDbContext.LoadTeamSprintTimeline(_dispatcher, _schedulingSettings, request.TeamId, tracked: false, cancellationToken);
        var metrics = new TeamSprintMetrics(request.SprintWindow ?? defaultWindow, timeline.ScheduleAt(now).SizingMethod);
        var candidates = metrics.Candidates(timeline, now);
        var sprintTypes = await _dispatcher.ResolveSprintTypes(candidates, cancellationToken);

        // Each sprint's history is its own read, so they stop as soon as the window is full.
        foreach (var sprint in candidates)
        {
            if (metrics.IsFull)
                break;

            if (!metrics.Admit(sprint, sprintTypes[sprint.Id]))
                continue;

            var history = await _workDbContext.LoadSprintScopePeriods(timeline, sprint, cancellationToken);
            var report = SprintScopeReport.Build(history.Window, history.SizingMethod, history.Periods, now);

            metrics.Assess(sprint, history.SizingMethod, history.HistoryIncomplete, report.Totals);
        }

        return TeamSprintMetricsDto.From(request.TeamId, metrics, defaultWindow);
    }
}
