using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events.Organization;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.PpmTeams.EventHandlers;

/// <summary>
/// Keeps the PPM module's <c>PpmTeam</c> copy of each Organization team (same Id).
/// </summary>
/// <remarks>
/// The <c>Team*</c> events are delivered durably (see <c>DurableEventRoutes</c>): at least once and in no
/// guaranteed order, with a retry-with-cooldown → dead-letter failure policy. This handler follows the rules
/// in "Consuming an event" (docs/contributing/domain-events.mdx): a change older than the one the copy already
/// holds is skipped, a missing copy is created from the team's current state, and a team that no longer
/// exists is left without a copy.
/// </remarks>
public sealed class PpmTeamChangeEventHandler(
    IProjectPortfolioManagementDbContext dbContext,
    IDispatcher dispatcher,
    ILogger<PpmTeamChangeEventHandler> logger)
{
    private readonly IProjectPortfolioManagementDbContext _dbContext = dbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ILogger<PpmTeamChangeEventHandler> _logger = logger;

    public async Task Handle(TeamCreatedEvent @event, CancellationToken cancellationToken)
    {
        if (await _dbContext.PpmTeams.AnyAsync(t => t.Id == @event.Id, cancellationToken))
        {
            _logger.LogInformation("[{SystemActionType}] Ppm Team create skipped: {PpmTeamId} already has a copy.", SystemActionType.ServiceDataReplication, @event.Id);
            return;
        }

        await CreateFromSource(@event.Id, @event.Timestamp, cancellationToken);
    }

    public async Task Handle(TeamDetailsUpdatedEvent @event, CancellationToken cancellationToken)
    {
        await Apply(@event.Id, @event.Timestamp, t => t.ApplyDetails(@event.Name, @event.Code, @event.Timestamp), "details", cancellationToken);
    }

    // Nothing raises the superseded type, but an envelope written as it before the switch can still be
    // waiting in the durable outbox; without this it would dead-letter rather than rename the copy.
#pragma warning disable CS0618
    public async Task Handle(TeamUpdatedEvent @event, CancellationToken cancellationToken)
#pragma warning restore CS0618
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
        var existingTeam = await _dbContext.PpmTeams.FirstOrDefaultAsync(t => t.Id == @event.Id, cancellationToken);
        if (existingTeam is null)
        {
            _logger.LogInformation("[{SystemActionType}] Ppm Team delete skipped: {PpmTeamId} has no copy.", SystemActionType.ServiceDataReplication, @event.Id);
            return;
        }

        _dbContext.PpmTeams.Remove(existingTeam);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Ppm Team deleted. {PpmTeamId}", SystemActionType.ServiceDataReplication, @event.Id);
    }

    private async Task Apply(Guid teamId, Instant timestamp, Func<PpmTeam, bool> apply, string change, CancellationToken cancellationToken)
    {
        var existingTeam = await _dbContext.PpmTeams.FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken);
        if (existingTeam is null)
        {
            await CreateFromSource(teamId, timestamp, cancellationToken);
            return;
        }

        if (!apply(existingTeam))
        {
            _logger.LogInformation("[{SystemActionType}] Ppm Team {Change} skipped: {PpmTeamId} already holds this or a newer change.", SystemActionType.ServiceDataReplication, change, teamId);
            return;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Ppm Team {Change} applied. {PpmTeamId}", SystemActionType.ServiceDataReplication, change, teamId);
    }

    private async Task CreateFromSource(Guid teamId, Instant timestamp, CancellationToken cancellationToken)
    {
        var source = await _dispatcher.Send(new GetSimpleTeamQuery(teamId), cancellationToken);
        if (source is null)
        {
            _logger.LogInformation("[{SystemActionType}] Ppm Team copy not created: team {PpmTeamId} no longer exists.", SystemActionType.ServiceDataReplication, teamId);
            return;
        }

        await _dbContext.PpmTeams.AddAsync(new PpmTeam(source, timestamp), cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Ppm Team created from source. {PpmTeamId}", SystemActionType.ServiceDataReplication, teamId);
    }
}
