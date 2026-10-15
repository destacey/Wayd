using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Iterations.Dtos;
using Wayd.Work.Application.Iterations.Sprints;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Domain.Models.SprintCapacity;
using Wayd.Work.Domain.Models.SprintScope;

namespace Wayd.Work.Application.Iterations.Queries;

/// <summary>
/// Gets a sprint's capacity over its planned dates beside its scope, and whether the scope is more than that
/// capacity is expected to deliver. Null when there is no such sprint, it has no team or no planned dates, or
/// the Team Capacity Management feature is off.
/// </summary>
public sealed record GetSprintCapacityQuery : IQuery<SprintCapacityDto?>
{
    public GetSprintCapacityQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Iteration>();
    }

    public Expression<Func<Iteration, bool>> IdOrKeyFilter { get; }
}

public sealed class GetSprintCapacityQueryHandler(
    IWorkDbContext workDbContext,
    IDispatcher dispatcher,
    ISettings<SchedulingSettings> schedulingSettings,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetSprintCapacityQuery, SprintCapacityDto?>
{
    /// <summary>
    /// How far back from the sprint's last day a prior sprint may start, in days, to be read with it: a capacity
    /// read spans no more than two years.
    /// </summary>
    private const int LookbackDays = 730;

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<SprintCapacityDto?> Handle(GetSprintCapacityQuery request, CancellationToken cancellationToken)
    {
        var sprint = await _workDbContext.Iterations
            .Where(request.IdOrKeyFilter)
            .Where(i => i.Type == IterationType.Sprint)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (sprint is null || sprint.TeamId is not { } teamId || sprint.DateRange.Start is not { } start || sprint.DateRange.End is not { } end)
            return null;

        var now = _dateTimeProvider.Now;
        var timeline = await _workDbContext.LoadTeamSprintTimeline(_dispatcher, _schedulingSettings, teamId, tracked: false, cancellationToken);
        var onTimeline = timeline.Sprints.Single(s => s.Id == sprint.Id);
        var window = SprintScopeWindow.For(timeline, onTimeline);
        var sizingMethod = timeline.ScheduleFor(onTimeline).SizingMethod;

        var (basis, scope) = now > window.Start
            ? (SprintCapacityScopeBasis.Committed, await CommittedScope(timeline, onTimeline, sizingMethod, now, cancellationToken))
            : (SprintCapacityScopeBasis.Planned, await PlannedScope(sprint.Id, sizingMethod, cancellationToken));

        // The same sprints, in the same unit, as the team's velocity is shown from.
        var metrics = await _dispatcher.Send(new GetTeamSprintMetricsQuery(teamId), cancellationToken);
        var earliest = end.PlusDays(-LookbackDays);
        List<TeamSprintMetricsSprintDto> prior = [.. (metrics?.Sprints ?? [])
            .Where(s => s.Id != sprint.Id && s.Start >= earliest && s.End <= end)
            .Where(s => sizingMethod == SizingMethod.Count || s.SizingMethod == sizingMethod)];

        // Each sprint's team days off are days nobody worked, as its ideal line has them.
        List<Guid> priorIds = [.. prior.Select(s => s.Id)];
        var priorDaysOff = (await _workDbContext.Iterations
            .Where(i => priorIds.Contains(i.Id))
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .ToDictionary(i => i.Id, i => i.TeamDaysOff);

        var capacity = await _dispatcher.Send(
            new GetTeamsCapacityByPeriodQuery(
                [teamId],
                [
                    new CapacityPeriod(start, end, sprint.TeamDaysOff),
                    .. prior.Select(s => new CapacityPeriod(s.Start, s.End, priorDaysOff.GetValueOrDefault(s.Id, []))),
                ]),
            cancellationToken);
        if (capacity is null || !capacity.TryGetValue(teamId, out var byPeriod))
            return null;

        // One entry per period, in the order asked for: this sprint first, then each prior sprint.
        var own = byPeriod[0];
        var comparison = SprintCapacityComparison.Compare(
            scope,
            own.DeliveryPersonDays,
            [.. prior.Select((s, i) => new PriorSprintCapacity(
                sizingMethod == SizingMethod.Count ? s.ByCount.Velocity : s.ByEstimate.Velocity,
                byPeriod[i + 1].DeliveryPersonDays))],
            isCompleted: now >= window.End);

        return new SprintCapacityDto
        {
            SprintId = sprint.Id,
            Start = start,
            End = end,
            AllocatedPersonDays = own.AllocatedPersonDays,
            DeliveryPersonDays = own.DeliveryPersonDays,
            AwayPersonDays = own.AwayPersonDays,
            SizingMethod = sizingMethod,
            ScopeBasis = basis,
            Scope = scope,
            Outcome = comparison.Outcome,
            ExpectedScope = comparison.ExpectedScope,
            PriorSprints = comparison.PriorSprints,
            IsOverCapacity = comparison.IsOverCapacity,
        };
    }

    /// <summary>What was in the sprint at its commitment point, as its scope report counts it.</summary>
    private async Task<double> CommittedScope(TeamSprintTimeline timeline, Iteration sprint, SizingMethod sizingMethod, Instant now, CancellationToken cancellationToken)
    {
        var history = await _workDbContext.LoadSprintScopePeriods(timeline, sprint, cancellationToken);
        var committed = SprintScopeReport.Build(history.Window, sizingMethod, history.Periods, now).Totals.Committed;

        return sizingMethod == SizingMethod.Count ? committed.Count : committed.Estimate;
    }

    /// <summary>The requirement-tier work in the sprint now, counted as its scope report counts it once committed.</summary>
    private async Task<double> PlannedScope(Guid sprintId, SizingMethod sizingMethod, CancellationToken cancellationToken)
    {
        var items = await _workDbContext.WorkItems
            .Where(w => w.IterationId == sprintId)
            .Where(w => w.Type.Level!.Tier == WorkTypeTier.Requirement)
            .Select(w => new { w.StoryPoints, w.Effort, w.Size })
            .ToListAsync(cancellationToken);

        return items.Sum(w => WorkItemEstimate.Of(sizingMethod, w.StoryPoints, w.Effort, w.Size) ?? 0);
    }
}
