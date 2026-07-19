using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events.Organization;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.PpmTeams.EventHandlers;

/// <summary>
/// Replicates Organization <c>Team</c> changes into the PPM <c>PpmTeam</c> projection (same Id).
/// </summary>
/// <remarks>
/// The <c>Team*</c> events are delivered durably (see <c>DurableEventRoutes</c>): enlisted in the Wolverine
/// outbox, delivered on a background thread, and governed by a retry-with-cooldown → dead-letter failure
/// policy. So this handler lets exceptions propagate — a transient DB failure is retried by Wolverine and a
/// poison message dead-letters. The per-message idempotency guards (create no-ops if the row already exists;
/// update/activate/deactivate/delete no-op if it does not) make redelivery safe.
/// </remarks>
public sealed class PpmTeamChangeEventHandler
{
    private readonly IProjectPortfolioManagementDbContext _dbContext;
    private readonly ILogger<PpmTeamChangeEventHandler> _logger;

    public PpmTeamChangeEventHandler(IProjectPortfolioManagementDbContext dbContext, ILogger<PpmTeamChangeEventHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Handle(TeamCreatedEvent @event, CancellationToken cancellationToken)
    {
        await CreatePpmTeam(@event, cancellationToken);
    }

    public async Task Handle(TeamUpdatedEvent @event, CancellationToken cancellationToken)
    {
        await UpdatePpmTeam(@event, cancellationToken);
    }

    public async Task Handle(TeamActivatedEvent @event, CancellationToken cancellationToken)
    {
        await ActivatePpmTeam(@event, cancellationToken);
    }

    public async Task Handle(TeamDeactivatedEvent @event, CancellationToken cancellationToken)
    {
        await DeactivatePpmTeam(@event, cancellationToken);
    }

    public async Task Handle(TeamDeletedEvent @event, CancellationToken cancellationToken)
    {
        await DeletePpmTeam(@event, cancellationToken);
    }

    private async Task CreatePpmTeam(TeamCreatedEvent team, CancellationToken cancellationToken)
    {
        if (await _dbContext.PpmTeams.AnyAsync(t => t.Id == team.Id, cancellationToken))
        {
            _logger.LogInformation("[{SystemActionType}] Ppm Team create action skipped. Team already exists. {PpmTeamId} - {PpmTeamName}", SystemActionType.ServiceDataReplication, team.Id, team.Name);
            return;
        }

        var ppmTeam = new PpmTeam(team);
        await _dbContext.PpmTeams.AddAsync(ppmTeam, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Ppm Team created. {PpmTeamId} - {PpmTeamName}", SystemActionType.ServiceDataReplication, team.Id, team.Name);
    }

    private async Task UpdatePpmTeam(TeamUpdatedEvent team, CancellationToken cancellationToken)
    {
        var existingTeam = await _dbContext.PpmTeams.FirstOrDefaultAsync(t => t.Id == team.Id, cancellationToken);
        if (existingTeam is null)
        {
            _logger.LogInformation("[{SystemActionType}] Ppm Team update action skipped. Unable to find ppm team {PpmTeamId} to update.", SystemActionType.ServiceDataReplication, team.Id);
            return;
        }

        existingTeam.Update(team);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Ppm Team updated. {PpmTeamId} - {PpmTeamName}", SystemActionType.ServiceDataReplication, team.Id, team.Name);
    }

    private async Task ActivatePpmTeam(TeamActivatedEvent team, CancellationToken cancellationToken)
    {
        var existingTeam = await _dbContext.PpmTeams.FirstOrDefaultAsync(t => t.Id == team.Id, cancellationToken);
        if (existingTeam is null)
        {
            _logger.LogInformation("[{SystemActionType}] Ppm Team activate action skipped. Unable to find ppm team {PpmTeamId} to activate.", SystemActionType.ServiceDataReplication, team.Id);
            return;
        }

        existingTeam.UpdateIsActive(true);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Ppm Team activated. {PpmTeamId} - {PpmTeamName}", SystemActionType.ServiceDataReplication, team.Id, existingTeam.Name);
    }

    private async Task DeactivatePpmTeam(TeamDeactivatedEvent team, CancellationToken cancellationToken)
    {
        var existingTeam = await _dbContext.PpmTeams.FirstOrDefaultAsync(t => t.Id == team.Id, cancellationToken);
        if (existingTeam is null)
        {
            _logger.LogInformation("[{SystemActionType}] Ppm Team deactivate action skipped. Unable to find ppm team {PpmTeamId} to deactivate.", SystemActionType.ServiceDataReplication, team.Id);
            return;
        }

        existingTeam.UpdateIsActive(false);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Ppm Team deactivated. {PpmTeamId} - {PpmTeamName}", SystemActionType.ServiceDataReplication, team.Id, existingTeam.Name);
    }

    private async Task DeletePpmTeam(TeamDeletedEvent team, CancellationToken cancellationToken)
    {
        var existingTeam = await _dbContext.PpmTeams.FirstOrDefaultAsync(t => t.Id == team.Id, cancellationToken);
        if (existingTeam is null)
        {
            _logger.LogInformation("[{SystemActionType}] Ppm Team delete action skipped. Unable to find ppm team {PpmTeamId} to delete.", SystemActionType.ServiceDataReplication, team.Id);
            return;
        }

        _dbContext.PpmTeams.Remove(existingTeam);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Ppm Team deleted. {PpmTeamId} - {PpmTeamName}", SystemActionType.ServiceDataReplication, existingTeam.Id, existingTeam.Name);
    }
}
