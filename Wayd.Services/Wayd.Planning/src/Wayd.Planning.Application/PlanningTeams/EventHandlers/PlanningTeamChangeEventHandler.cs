using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events.Organization;

namespace Wayd.Planning.Application.PlanningTeams.EventHandlers;

/// <summary>
/// Keeps the Planning module's <c>PlanningTeam</c> copy of each Organization team (same Id).
/// </summary>
/// <remarks>
/// The <c>Team*</c> events are delivered durably (see <c>DurableEventRoutes</c>): at least once and in no
/// guaranteed order, with a retry-with-cooldown → dead-letter failure policy. This handler follows the rules
/// in "Consuming an event" (docs/contributing/domain-events.mdx): a change older than the one the copy already
/// holds is skipped, a missing copy is created from the team's current state, and a team that no longer
/// exists is left without a copy.
/// <para>
/// Because delivery is asynchronous, a <c>PlanningTeam</c> may not exist at the instant a follow-up command
/// references it. <c>ManagePlanningIntervalTeamsCommand</c> validates team existence and fails cleanly rather
/// than FK-faulting, which is what makes async replication safe for this copy.
/// </para>
/// </remarks>
public sealed class PlanningTeamChangeEventHandler(
    IPlanningDbContext planningDbContext,
    IDispatcher dispatcher,
    ILogger<PlanningTeamChangeEventHandler> logger)
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ILogger<PlanningTeamChangeEventHandler> _logger = logger;

    public async Task Handle(TeamCreatedEvent @event, CancellationToken cancellationToken)
    {
        if (await _planningDbContext.PlanningTeams.AnyAsync(t => t.Id == @event.Id, cancellationToken))
        {
            _logger.LogInformation("[{SystemActionType}] Planning Team create skipped: {PlanningTeamId} already has a copy.", SystemActionType.ServiceDataReplication, @event.Id);
            return;
        }

        await CreateFromSource(@event.Id, @event.Timestamp, cancellationToken);
    }

    public async Task Handle(TeamUpdatedEvent @event, CancellationToken cancellationToken)
    {
        await Apply(@event.Id, @event.Timestamp, t => t.ApplyDetails(@event.Name, @event.Code, @event.Timestamp), "details", cancellationToken);
    }

    public async Task Handle(TeamActivatedEvent @event, CancellationToken cancellationToken)
    {
        await Apply(@event.Id, @event.Timestamp, t => t.ApplyActivation(true, @event.Timestamp), "activation", cancellationToken);
    }

    public async Task Handle(TeamDeactivatedEvent @event, CancellationToken cancellationToken)
    {
        await Apply(@event.Id, @event.Timestamp, t => t.ApplyActivation(false, @event.Timestamp), "deactivation", cancellationToken);
    }

    public async Task Handle(TeamDeletedEvent @event, CancellationToken cancellationToken)
    {
        var existingTeam = await _planningDbContext.PlanningTeams.FirstOrDefaultAsync(t => t.Id == @event.Id, cancellationToken);
        if (existingTeam is null)
        {
            _logger.LogInformation("[{SystemActionType}] Planning Team delete skipped: {PlanningTeamId} has no copy.", SystemActionType.ServiceDataReplication, @event.Id);
            return;
        }

        // TODO: consider making the team inactive or archiving it instead of deleting it.  Maybe we only delete if the planning team has never been used?
        _planningDbContext.PlanningTeams.Remove(existingTeam);
        await _planningDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Planning Team deleted. {PlanningTeamId}", SystemActionType.ServiceDataReplication, @event.Id);
    }

    private async Task Apply(Guid teamId, Instant timestamp, Func<PlanningTeam, bool> apply, string change, CancellationToken cancellationToken)
    {
        var existingTeam = await _planningDbContext.PlanningTeams.FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken);
        if (existingTeam is null)
        {
            await CreateFromSource(teamId, timestamp, cancellationToken);
            return;
        }

        if (!apply(existingTeam))
        {
            _logger.LogInformation("[{SystemActionType}] Planning Team {Change} skipped: {PlanningTeamId} already holds this or a newer change.", SystemActionType.ServiceDataReplication, change, teamId);
            return;
        }

        await _planningDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Planning Team {Change} applied. {PlanningTeamId}", SystemActionType.ServiceDataReplication, change, teamId);
    }

    private async Task CreateFromSource(Guid teamId, Instant timestamp, CancellationToken cancellationToken)
    {
        var source = await _dispatcher.Send(new GetSimpleTeamQuery(teamId), cancellationToken);
        if (source is null)
        {
            _logger.LogInformation("[{SystemActionType}] Planning Team copy not created: team {PlanningTeamId} no longer exists.", SystemActionType.ServiceDataReplication, teamId);
            return;
        }

        await _planningDbContext.PlanningTeams.AddAsync(new PlanningTeam(source, timestamp), cancellationToken);
        await _planningDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Planning Team created from source. {PlanningTeamId}", SystemActionType.ServiceDataReplication, teamId);
    }
}
