using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events.Planning.PlanningIntervals;
using Wayd.Planning.Application.PlanningIntervals.SprintMapping;

namespace Wayd.Planning.Application.PlanningIntervals.EventHandlers;

/// <summary>
/// Maps a Scrum team's sprints to a planning interval's iterations when the team joins it, so they need not be
/// mapped by hand. Matching follows <see cref="IterationSprintMatcher"/>.
/// </summary>
/// <remarks>
/// <para>
/// Delivered durably (see <c>DurableEventRoutes</c>), so it runs after the team was added and may run more than
/// once. It only fills: an iteration the team already has a sprint in, and a sprint already mapped in any planning
/// interval, are left alone, so a redelivery changes nothing and a later manual mapping is never overwritten.
/// </para>
/// <para>
/// <c>PlanningInterval</c> has no concurrency token, so an edit saved between this handler's read and its save is
/// not detected. A team removed in that window is left with the mappings made here, in an interval it is no
/// longer part of; the window is the few milliseconds between the handler's read and its save.
/// </para>
/// <para>
/// Only a team, never a team of teams, has sprints. It counts as Scrum for an iteration when the operating model
/// in effect on the iteration's first day is Scrum, so a team that changes methodology during the interval is
/// matched only for the part it ran Scrum in.
/// </para>
/// </remarks>
public sealed class TeamSprintAutoMappingHandler(
    IPlanningDbContext planningDbContext,
    IDispatcher dispatcher,
    IDateTimeProvider dateTimeProvider,
    ILogger<TeamSprintAutoMappingHandler> logger)
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<TeamSprintAutoMappingHandler> _logger = logger;

    public async Task Handle(PlanningIntervalTeamsChangedEvent @event, CancellationToken cancellationToken)
    {
        if (@event.Added.Length == 0)
            return;

        var planningInterval = await _planningDbContext.PlanningIntervals
            .Include(pi => pi.Teams)
            .Include(pi => pi.Iterations)
            .Include(pi => pi.IterationSprints)
                .ThenInclude(s => s.Sprint)
            .SingleOrDefaultAsync(pi => pi.Id == @event.Id, cancellationToken);

        if (planningInterval is null)
        {
            _logger.LogInformation("Sprint auto-mapping skipped: Planning Interval {PlanningIntervalId} no longer exists.", @event.Id);
            return;
        }

        // A team removed again before this ran has nothing to map to, and a team of teams has no sprints.
        var stillInInterval = @event.Added
            .Where(id => planningInterval.Teams.Any(t => t.TeamId == id))
            .Distinct()
            .ToList();
        var teamIds = await _planningDbContext.PlanningTeams
            .Where(t => stillInInterval.Contains(t.Id) && t.Type == TeamType.Team)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
        if (teamIds.Count == 0)
            return;

        var schedules = await _dispatcher.Send(new GetTeamsScheduleHistoryQuery(teamIds), cancellationToken);

        var piStart = planningInterval.DateRange.Start;
        var piEnd = planningInterval.DateRange.End;
        // A sprint can be mapped in one planning interval only (a unique index), counting a deleted one, which
        // keeps its mappings; mapping it again would fail the save on every retry.
        var sprints = await _planningDbContext.PlanningSprints
            .Where(s => s.TeamId.HasValue && teamIds.Contains(s.TeamId.Value)
                && s.Type == IterationType.Sprint
                && s.DateRange.Start <= piEnd
                && s.DateRange.End >= piStart
                && !_planningDbContext.PlanningIntervalIterationSprints.Any(m => m.SprintId == s.Id))
            .ToListAsync(cancellationToken);

        var actor = ActorFor(@event.Actor);
        var timestamp = _dateTimeProvider.Now;
        var mappedTeams = 0;

        foreach (var teamId in teamIds)
        {
            if (!schedules.TryGetValue(teamId, out var periods))
                continue;

            var teamMappings = planningInterval.IterationSprints
                .Where(s => s.Sprint?.TeamId == teamId)
                .ToList();

            // The team's mappings go back through the aggregate's validation below, and a sprint that has since
            // stopped being a Sprint would fail it on every delivery. Such a team is left for mapping by hand.
            if (teamMappings.Any(m => m.Sprint!.Type != IterationType.Sprint))
            {
                _logger.LogWarning("Sprint auto-mapping skipped team {TeamId} in Planning Interval {PlanningIntervalId}: one of its mapped sprints is no longer a sprint.", teamId, @event.Id);
                continue;
            }

            var openIterations = planningInterval.Iterations
                .Where(i => teamMappings.All(m => m.PlanningIntervalIterationId != i.Id))
                .Where(i => periods.FirstOrDefault(p => p.IncludesDate(i.DateRange.Start))?.Methodology == Methodology.Scrum);
            var mappedSprintIds = planningInterval.IterationSprints.Select(s => s.SprintId).ToHashSet();
            var openSprints = sprints
                .Where(s => s.TeamId == teamId && !mappedSprintIds.Contains(s.Id))
                .ToList();

            var matches = IterationSprintMatcher.Match(openIterations, openSprints);
            if (matches.Count == 0)
                continue;

            // SyncTeamSprintMappings replaces the team's whole set, so what the team already has goes in with
            // the matches.
            var desired = teamMappings.ToDictionary(m => m.PlanningIntervalIterationId, m => (Guid?)m.SprintId);
            foreach (var (iterationId, sprintId) in matches)
                desired[iterationId] = sprintId;

            var available = teamMappings
                .Select(m => m.Sprint!)
                .Concat(openSprints)
                .DistinctBy(s => s.Id)
                .ToDictionary(s => s.Id);

            var result = planningInterval.SyncTeamSprintMappings(teamId, desired, available, actor, timestamp);
            if (result.IsFailure)
            {
                // The aggregate may be part-way through the change, so nothing is saved.
                _logger.LogWarning("Sprint auto-mapping for Planning Interval {PlanningIntervalId} abandoned: team {TeamId} could not be mapped: {Error}", @event.Id, teamId, result.Error);
                return;
            }

            mappedTeams++;
        }

        if (mappedTeams == 0)
            return;

        await _planningDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Sprint auto-mapping mapped sprints for {TeamCount} team(s) in Planning Interval {PlanningIntervalId}.", mappedTeams, @event.Id);
    }

    /// <summary>
    /// Automation started by whoever added the team. Only a direct user action names an employee: an import's
    /// employee is the person its row is about, not the person who ran it.
    /// </summary>
    private static EventActor ActorFor(EventActor trigger) => trigger switch
    {
        { Kind: EventActorKind.User } => EventActor.Automation(trigger.UserId, trigger.EmployeeId),
        { HasOriginatingUser: true } => EventActor.Automation(trigger.UserId),
        _ => EventActor.System,
    };
}
