using Wayd.Common.Application.Events;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events.Organization;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.PpmTeams.EventHandlers;

internal sealed class PpmTeamChangeEventHandler :
    IEventNotificationHandler<TeamCreatedEvent>,
    IEventNotificationHandler<TeamUpdatedEvent>,
    IEventNotificationHandler<TeamActivatedEvent>,
    IEventNotificationHandler<TeamDeactivatedEvent>,
    IEventNotificationHandler<TeamDeletedEvent>
{
    private readonly IProjectPortfolioManagementDbContext _dbContext;
    private readonly ILogger<PpmTeamChangeEventHandler> _logger;

    public PpmTeamChangeEventHandler(IProjectPortfolioManagementDbContext dbContext, ILogger<PpmTeamChangeEventHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Handle(EventNotification<TeamCreatedEvent> notification, CancellationToken cancellationToken)
    {
        await CreatePpmTeam(notification.Event, cancellationToken);
    }

    public async Task Handle(EventNotification<TeamUpdatedEvent> notification, CancellationToken cancellationToken)
    {
        await UpdatePpmTeam(notification.Event, cancellationToken);
    }

    public async Task Handle(EventNotification<TeamActivatedEvent> notification, CancellationToken cancellationToken)
    {
        await ActivatePpmTeam(notification.Event, cancellationToken);
    }

    public async Task Handle(EventNotification<TeamDeactivatedEvent> notification, CancellationToken cancellationToken)
    {
        await DeactivatePpmTeam(notification.Event, cancellationToken);
    }

    public async Task Handle(EventNotification<TeamDeletedEvent> notification, CancellationToken cancellationToken)
    {
        await DeletePpmTeam(notification.Event, cancellationToken);
    }

    private async Task CreatePpmTeam(TeamCreatedEvent team, CancellationToken cancellationToken)
    {
        if (await _dbContext.PpmTeams.AnyAsync(t => t.Id == team.Id, cancellationToken))
        {
            _logger.LogInformation("[{SystemActionType}] Ppm Team create action skipped. Team already exists. {PpmTeamId} - {PpmTeamName}", SystemActionType.ServiceDataReplication, team.Id, team.Name);
            return;
        }

        var ppmTeam = new PpmTeam(team);
        try
        {
            await _dbContext.PpmTeams.AddAsync(ppmTeam, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("[{SystemActionType}] Ppm Team created. {PpmTeamId} - {PpmTeamName}", SystemActionType.ServiceDataReplication, team.Id, team.Name);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "[{SystemActionType}] Ppm Team create action failed to save. {PpmTeamId} - {PpmTeamName}", SystemActionType.ServiceDataReplication, team.Id, team.Name);
        }
    }

    private async Task UpdatePpmTeam(TeamUpdatedEvent team, CancellationToken cancellationToken)
    {
        var existingTeam = await _dbContext.PpmTeams.FirstOrDefaultAsync(t => t.Id == team.Id, cancellationToken);
        if (existingTeam is null)
        {
            _logger.LogInformation("[{SystemActionType}] Ppm Team update action skipped. Unable to find ppm team {PpmTeamId} to update.", SystemActionType.ServiceDataReplication, team.Id);
            return;
        }
        try
        {
            existingTeam.Update(team);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("[{SystemActionType}] Ppm Team updated. {PpmTeamId} - {PpmTeamName}", SystemActionType.ServiceDataReplication, team.Id, team.Name);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "[{SystemActionType}] Ppm Team update action failed to save. {PpmTeamId} - {PpmTeamName}", SystemActionType.ServiceDataReplication, team.Id, team.Name);
        }
    }

    private async Task ActivatePpmTeam(TeamActivatedEvent team, CancellationToken cancellationToken)
    {
        var existingTeam = await _dbContext.PpmTeams.FirstOrDefaultAsync(t => t.Id == team.Id, cancellationToken);
        if (existingTeam is null)
        {
            _logger.LogInformation("[{SystemActionType}] Ppm Team activate action skipped. Unable to find ppm team {PpmTeamId} to activate.", SystemActionType.ServiceDataReplication, team.Id);
            return;
        }
        try
        {
            existingTeam.UpdateIsActive(true);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("[{SystemActionType}] Ppm Team activated. {PpmTeamId} - {PpmTeamName}", SystemActionType.ServiceDataReplication, team.Id, existingTeam.Name);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "[{SystemActionType}] Ppm Team activate action failed to save. {PpmTeamId} - {PpmTeamName}", SystemActionType.ServiceDataReplication, team.Id, existingTeam.Name);
        }
    }

    private async Task DeactivatePpmTeam(TeamDeactivatedEvent team, CancellationToken cancellationToken)
    {
        var existingTeam = await _dbContext.PpmTeams.FirstOrDefaultAsync(t => t.Id == team.Id, cancellationToken);
        if (existingTeam is null)
        {
            _logger.LogInformation("[{SystemActionType}] Ppm Team deactivate action skipped. Unable to find ppm team {PpmTeamId} to deactivate.", SystemActionType.ServiceDataReplication, team.Id);
            return;
        }
        try
        {
            existingTeam.UpdateIsActive(false);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("[{SystemActionType}] Ppm Team deactivated. {PpmTeamId} - {PpmTeamName}", SystemActionType.ServiceDataReplication, team.Id, existingTeam.Name);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "[{SystemActionType}] Ppm Team deactivate action failed to save. {PpmTeamId} - {PpmTeamName}", SystemActionType.ServiceDataReplication, team.Id, existingTeam.Name);
        }
    }

    private async Task DeletePpmTeam(TeamDeletedEvent team, CancellationToken cancellationToken)
    {
        var existingTeam = await _dbContext.PpmTeams.FirstOrDefaultAsync(t => t.Id == team.Id, cancellationToken);

        try
        {
            if (existingTeam is null)
            {
                _logger.LogInformation("[{SystemActionType}] Ppm Team deleted. Unable to find ppm team {PpmTeamId} to delete.", SystemActionType.ServiceDataReplication, team.Id);
            }
            else
            {
                _dbContext.PpmTeams.Remove(existingTeam);
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("[{SystemActionType}] Ppm Team deleted. {PpmTeamId} - {PpmTeamName}", SystemActionType.ServiceDataReplication, existingTeam.Id, existingTeam.Name);
            }

        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "[{SystemActionType}] Ppm Team delete action failed to save. {PpmTeamId} - {PpmTeamName}", SystemActionType.ServiceDataReplication, team.Id, existingTeam?.Name ?? "Unknown Team");
        }
    }
}
