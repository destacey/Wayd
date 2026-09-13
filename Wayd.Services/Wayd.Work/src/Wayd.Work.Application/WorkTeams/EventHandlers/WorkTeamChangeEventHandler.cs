using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkTeams.EventHandlers;

/// <summary>
/// Keeps the Work module's <c>WorkTeam</c> copy of each Organization team (same Id).
/// </summary>
/// <remarks>
/// The <c>Team*</c> events are delivered durably (see <c>DurableEventRoutes</c>): at least once and in no
/// guaranteed order, with a retry-with-cooldown → dead-letter failure policy. This handler follows the rules
/// in "Consuming an event" (docs/contributing/domain-events.mdx): a change older than the one the copy already
/// holds is skipped, a missing copy is created from the team's current state, and a team that no longer
/// exists is left without a copy.
/// </remarks>
public sealed class WorkTeamChangeEventHandler(
    IWorkDbContext workDbContext,
    IDispatcher dispatcher,
    ILogger<WorkTeamChangeEventHandler> logger)
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ILogger<WorkTeamChangeEventHandler> _logger = logger;

    public async Task Handle(TeamCreatedEvent @event, CancellationToken cancellationToken)
    {
        if (await _workDbContext.WorkTeams.AnyAsync(t => t.Id == @event.Id, cancellationToken))
        {
            _logger.LogInformation("[{SystemActionType}] Work Team create skipped: {WorkTeamId} already has a copy.", SystemActionType.ServiceDataReplication, @event.Id);
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
        var existingTeam = await _workDbContext.WorkTeams.FirstOrDefaultAsync(t => t.Id == @event.Id, cancellationToken);
        if (existingTeam is null)
        {
            _logger.LogInformation("[{SystemActionType}] Work Team delete skipped: {WorkTeamId} has no copy.", SystemActionType.ServiceDataReplication, @event.Id);
            return;
        }

        // TODO: consider making the team inactive or archiving it instead of deleting it.  Maybe we only delete if the Work team has never been used?
        _workDbContext.WorkTeams.Remove(existingTeam);
        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Work Team deleted. {WorkTeamId}", SystemActionType.ServiceDataReplication, @event.Id);
    }

    private async Task Apply(Guid teamId, Instant timestamp, Func<WorkTeam, bool> apply, string change, CancellationToken cancellationToken)
    {
        var existingTeam = await _workDbContext.WorkTeams.FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken);
        if (existingTeam is null)
        {
            await CreateFromSource(teamId, timestamp, cancellationToken);
            return;
        }

        if (!apply(existingTeam))
        {
            _logger.LogInformation("[{SystemActionType}] Work Team {Change} skipped: {WorkTeamId} already holds this or a newer change.", SystemActionType.ServiceDataReplication, change, teamId);
            return;
        }

        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Work Team {Change} applied. {WorkTeamId}", SystemActionType.ServiceDataReplication, change, teamId);
    }

    private async Task CreateFromSource(Guid teamId, Instant timestamp, CancellationToken cancellationToken)
    {
        var source = await _dispatcher.Send(new GetSimpleTeamQuery(teamId), cancellationToken);
        if (source is null)
        {
            _logger.LogInformation("[{SystemActionType}] Work Team copy not created: team {WorkTeamId} no longer exists.", SystemActionType.ServiceDataReplication, teamId);
            return;
        }

        await _workDbContext.WorkTeams.AddAsync(new WorkTeam(source, timestamp), cancellationToken);
        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Work Team created from source. {WorkTeamId}", SystemActionType.ServiceDataReplication, teamId);
    }
}
