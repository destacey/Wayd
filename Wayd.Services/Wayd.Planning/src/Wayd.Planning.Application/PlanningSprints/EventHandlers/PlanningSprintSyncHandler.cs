using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Common.Domain.Interfaces.Planning.Iterations;
using Wayd.Common.Domain.Models.Planning.Iterations;

namespace Wayd.Planning.Application.PlanningSprints.EventHandlers;

/// <summary>
/// Keeps the Planning module's <c>PlanningSprint</c> copy of each sprint (same Id).
/// </summary>
/// <remarks>
/// The <c>Iteration*</c> events are delivered durably (see <c>DurableEventRoutes</c>): at least once and in no
/// guaranteed order, with a retry-with-cooldown → dead-letter failure policy. This handler follows the rules
/// in "Consuming an event" (docs/contributing/domain-events.mdx): a change older than the one the copy already
/// holds is skipped, a missing copy is created from the sprint's current state, and a sprint that no longer
/// exists is left without a copy.
/// </remarks>
public sealed class PlanningSprintSyncHandler(
    IPlanningDbContext planningDbContext,
    IDispatcher dispatcher,
    ILogger<PlanningSprintSyncHandler> logger)
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ILogger<PlanningSprintSyncHandler> _logger = logger;

    public async Task Handle(IterationCreatedEventV3 @event, CancellationToken cancellationToken)
    {
        await Create(@event.Id, @event.Timestamp, cancellationToken);
    }

    public async Task Handle(IterationDetailsUpdatedEvent @event, CancellationToken cancellationToken)
    {
        await Apply(@event.Id, @event.Timestamp, s => s.ApplyDetails(@event.Name, @event.Type, @event.Timestamp), "details", cancellationToken);
    }

    public async Task Handle(IterationDateRangeChangedEventV2 @event, CancellationToken cancellationToken)
    {
        await Apply(@event.Id, @event.Timestamp, s => s.ApplyDateRange(@event.DateRange, @event.Timestamp), "date range", cancellationToken);
    }

    public async Task Handle(IterationTeamChangedEvent @event, CancellationToken cancellationToken)
    {
        await Apply(@event.Id, @event.Timestamp, s => s.ApplyTeam(@event.TeamId, @event.Timestamp), "team", cancellationToken);
    }

    // Nothing raises the superseded types below, but an envelope written as one before the switch can still be
    // waiting in the durable outbox; without these it would dead-letter rather than update the copy.
#pragma warning disable CS0618
    public async Task Handle(IterationCreatedEventV2 @event, CancellationToken cancellationToken)
    {
        await Create(@event.Id, @event.Timestamp, cancellationToken);
    }

    public async Task Handle(IterationCreatedEvent @event, CancellationToken cancellationToken)
    {
        await Create(@event.Id, @event.Timestamp, cancellationToken);
    }

    // The copy no longer holds a state: Planning reads it from Work when needed.
    public Task Handle(IterationStateChangedEvent @event, CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task Handle(IterationDateRangeChangedEvent @event, CancellationToken cancellationToken)
    {
        await Apply(@event.Id, @event.Timestamp, s => s.ApplyDateRange(@event.DateRange.ToIterationDateRange(), @event.Timestamp), "date range", cancellationToken);
    }

    public async Task Handle(IterationUpdatedEvent @event, CancellationToken cancellationToken)
    {
        var record = new SupersededRecord(@event.Id, @event.Key, @event.Name, @event.Type, @event.DateRange.ToIterationDateRange(), @event.TeamId);
        await Apply(@event.Id, @event.Timestamp, s => s.ApplyRecord(record, @event.Timestamp), "record", cancellationToken);
    }
#pragma warning restore CS0618

    public async Task Handle(IterationDeletedEvent @event, CancellationToken cancellationToken)
    {
        var sprint = await _planningDbContext.PlanningSprints.FirstOrDefaultAsync(x => x.Id == @event.Id, cancellationToken);
        if (sprint == null)
        {
            _logger.LogInformation("Planning {SystemActionType} for a deleted Sprint skipped: Sprint {SprintId} has no copy.", SystemActionType.ServiceDataReplication, @event.Id);
            return;
        }

        // The sprint is gone at its source, so a PI can no longer count it.
        var unmappedFrom = await _planningDbContext.UnmapFromPlanningIntervals([@event.Id], EventActor.System, @event.Timestamp, cancellationToken);

        _planningDbContext.PlanningSprints.Remove(sprint);
        await _planningDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Planning {SystemActionType} for the Sprint {SprintId} deleted action. Unmapped from {PlanningIntervalCount} planning intervals.", SystemActionType.ServiceDataReplication, @event.Id, unmappedFrom);
    }

    private async Task Create(Guid sprintId, Instant timestamp, CancellationToken cancellationToken)
    {
        if (await _planningDbContext.PlanningSprints.AnyAsync(x => x.Id == sprintId, cancellationToken))
        {
            _logger.LogInformation("Planning {SystemActionType} for a new Sprint skipped: Sprint {SprintId} already has a copy.", SystemActionType.ServiceDataReplication, sprintId);
            return;
        }

        await CreateFromSource(sprintId, timestamp, cancellationToken);
    }

    private async Task Apply(Guid sprintId, Instant timestamp, Func<PlanningSprint, bool> apply, string change, CancellationToken cancellationToken)
    {
        var sprint = await _planningDbContext.PlanningSprints.FirstOrDefaultAsync(x => x.Id == sprintId, cancellationToken);
        if (sprint == null)
        {
            await CreateFromSource(sprintId, timestamp, cancellationToken);
            return;
        }

        var previousTeamId = sprint.TeamId;
        if (!apply(sprint))
        {
            _logger.LogInformation("Planning {SystemActionType} for a Sprint {Change} skipped: Sprint {SprintId} already holds this or a newer change.", SystemActionType.ServiceDataReplication, change, sprintId);
            return;
        }

        // A mapping is the team's: one sprint per team per PI iteration, for a team in the PI. A sprint that
        // changes team no longer answers for the old one, so it leaves every PI it was mapped in.
        if (sprint.TeamId != previousTeamId)
        {
            await _planningDbContext.UnmapFromPlanningIntervals([sprintId], EventActor.System, timestamp, cancellationToken);
        }

        await _planningDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Planning {SystemActionType} for the Sprint {SprintId} {Change}.", SystemActionType.ServiceDataReplication, sprintId, change);
    }

    private async Task CreateFromSource(Guid sprintId, Instant timestamp, CancellationToken cancellationToken)
    {
        var source = await _dispatcher.Send(new GetSimpleIterationQuery(sprintId), cancellationToken);
        if (source is null)
        {
            _logger.LogInformation("Planning {SystemActionType} copy not created: Sprint {SprintId} no longer exists.", SystemActionType.ServiceDataReplication, sprintId);
            return;
        }

        await _planningDbContext.PlanningSprints.AddAsync(new PlanningSprint(source, timestamp), cancellationToken);
        await _planningDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful Planning {SystemActionType} creating Sprint {SprintId} from its source.", SystemActionType.ServiceDataReplication, sprintId);
    }

    private sealed record SupersededRecord(Guid Id, int Key, string Name, IterationType Type, IterationDateRange DateRange, Guid? TeamId) : ISimpleIteration;
}
