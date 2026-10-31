using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events.Organization;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.ProductTeams.EventHandlers;

/// <summary>
/// Keeps the Product Management module's <c>ProductTeam</c> copy of each Organization team (same Id).
/// </summary>
/// <remarks>
/// The <c>Team*</c> events are delivered durably (see <c>DurableEventRoutes</c>): at least once and in no
/// guaranteed order, with a retry-with-cooldown → dead-letter failure policy. This handler follows the rules
/// in "Consuming an event" (docs/contributing/domain-events.mdx): a change older than the one the copy already
/// holds is skipped, a missing copy is created from the team's current state, and a team that no longer
/// exists is left without a copy.
/// </remarks>
public sealed class ProductTeamChangeEventHandler(
    IProductManagementDbContext dbContext,
    IDispatcher dispatcher,
    ILogger<ProductTeamChangeEventHandler> logger)
{
    private readonly IProductManagementDbContext _dbContext = dbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ILogger<ProductTeamChangeEventHandler> _logger = logger;

    public async Task Handle(TeamCreatedEvent @event, CancellationToken cancellationToken)
    {
        if (await _dbContext.ProductTeams.AnyAsync(t => t.Id == @event.Id, cancellationToken))
        {
            _logger.LogInformation("[{SystemActionType}] Product Team create skipped: {ProductTeamId} already has a copy.", SystemActionType.ServiceDataReplication, @event.Id);
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

    private async Task Apply(Guid teamId, Instant timestamp, Func<ProductTeam, bool> apply, string change, CancellationToken cancellationToken)
    {
        var existingTeam = await _dbContext.ProductTeams.FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken);
        if (existingTeam is null)
        {
            await CreateFromSource(teamId, timestamp, cancellationToken);
            return;
        }

        if (!apply(existingTeam))
        {
            _logger.LogInformation("[{SystemActionType}] Product Team {Change} skipped: {ProductTeamId} already holds this or a newer change.", SystemActionType.ServiceDataReplication, change, teamId);
            return;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Product Team {Change} applied. {ProductTeamId}", SystemActionType.ServiceDataReplication, change, teamId);
    }

    private async Task CreateFromSource(Guid teamId, Instant timestamp, CancellationToken cancellationToken)
    {
        var source = await _dispatcher.Send(new GetSimpleTeamQuery(teamId), cancellationToken);
        if (source is null)
        {
            _logger.LogInformation("[{SystemActionType}] Product Team copy not created: team {ProductTeamId} no longer exists.", SystemActionType.ServiceDataReplication, teamId);
            return;
        }

        await _dbContext.ProductTeams.AddAsync(new ProductTeam(source, timestamp), cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Product Team created from source. {ProductTeamId}", SystemActionType.ServiceDataReplication, teamId);
    }
}
