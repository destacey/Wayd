using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkTeams.EventHandlers;

public sealed class WorkTeamChangeEventHandler
{
    private readonly IWorkDbContext _workDbContext;
    private readonly ILogger<WorkTeamChangeEventHandler> _logger;

    public WorkTeamChangeEventHandler(IWorkDbContext workDbContext, ILogger<WorkTeamChangeEventHandler> logger)
    {
        _workDbContext = workDbContext;
        _logger = logger;
    }

    public async Task Handle(TeamCreatedEvent @event, CancellationToken cancellationToken)
    {
        await CreateWorkTeam(@event, cancellationToken);
    }

    public async Task Handle(TeamUpdatedEvent @event, CancellationToken cancellationToken)
    {
        await UpdateWorkTeam(@event, cancellationToken);
    }

    public async Task Handle(TeamActivatedEvent @event, CancellationToken cancellationToken)
    {
        await ActivateWorkTeam(@event, cancellationToken);
    }

    public async Task Handle(TeamDeactivatedEvent @event, CancellationToken cancellationToken)
    {
        await DeactivateWorkTeam(@event, cancellationToken);
    }

    public async Task Handle(TeamDeletedEvent @event, CancellationToken cancellationToken)
    {
        await DeleteWorkTeam(@event, cancellationToken);
    }

    private async Task CreateWorkTeam(TeamCreatedEvent team, CancellationToken cancellationToken)
    {
        if (await _workDbContext.WorkTeams.AnyAsync(t => t.Id == team.Id, cancellationToken))
        {
            _logger.LogInformation("[{SystemActionType}] Work Team create action skipped. Team already exists. {WorkTeamId} - {WorkTeamName}", SystemActionType.ServiceDataReplication, team.Id, team.Name);
            return;
        }

        var WorkTeam = new WorkTeam(team);
        try
        {
            await _workDbContext.WorkTeams.AddAsync(WorkTeam, cancellationToken);
            await _workDbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("[{SystemActionType}] Work Team created. {WorkTeamId} - {WorkTeamName}", SystemActionType.ServiceDataReplication, team.Id, team.Name);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "[{SystemActionType}] Work Team create action failed to save. {WorkTeamId} - {WorkTeamName}", SystemActionType.ServiceDataReplication, team.Id, team.Name);
        }
    }

    private async Task UpdateWorkTeam(TeamUpdatedEvent team, CancellationToken cancellationToken)
    {
        var existingTeam = await _workDbContext.WorkTeams.FirstOrDefaultAsync(t => t.Id == team.Id, cancellationToken);
        if (existingTeam is null)
        {
            _logger.LogInformation("[{SystemActionType}] Work Team update action skipped. Unable to find work team {WorkTeamId} to update.", SystemActionType.ServiceDataReplication, team.Id);
            return;
        }
        try
        {
            existingTeam.Update(team);
            await _workDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("[{SystemActionType}] Work Team updated. {WorkTeamId} - {WorkTeamName}", SystemActionType.ServiceDataReplication, team.Id, team.Name);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "[{SystemActionType}] Work Team update action failed to save. {WorkTeamId} - {WorkTeamName}", SystemActionType.ServiceDataReplication, team.Id, team.Name);
        }
    }

    private async Task ActivateWorkTeam(TeamActivatedEvent team, CancellationToken cancellationToken)
    {
        var existingTeam = await _workDbContext.WorkTeams.FirstOrDefaultAsync(t => t.Id == team.Id, cancellationToken);
        if (existingTeam is null)
        {
            _logger.LogInformation("[{SystemActionType}] Work Team activate action skipped. Unable to find work team {WorkTeamId} to activate.", SystemActionType.ServiceDataReplication, team.Id);
            return;
        }
        try
        {
            existingTeam.UpdateIsActive(true);
            await _workDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("[{SystemActionType}] Work Team activated. {WorkTeamId} - {WorkTeamName}", SystemActionType.ServiceDataReplication, team.Id, existingTeam.Name);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "[{SystemActionType}] Work Team activate action failed to save. {WorkTeamId} - {WorkTeamName}", SystemActionType.ServiceDataReplication, team.Id, existingTeam.Name);
        }
    }

    private async Task DeactivateWorkTeam(TeamDeactivatedEvent team, CancellationToken cancellationToken)
    {
        var existingTeam = await _workDbContext.WorkTeams.FirstOrDefaultAsync(t => t.Id == team.Id, cancellationToken);
        if (existingTeam is null)
        {
            _logger.LogInformation("[{SystemActionType}] Work Team deactivate action skipped. Unable to find work team {WorkTeamId} to deactivate.", SystemActionType.ServiceDataReplication, team.Id);
            return;
        }
        try
        {
            existingTeam.UpdateIsActive(false);
            await _workDbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("[{SystemActionType}] Work Team deactivated. {WorkTeamId} - {WorkTeamName}", SystemActionType.ServiceDataReplication, team.Id, existingTeam.Name);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "[{SystemActionType}] Work Team deactivate action failed to save. {WorkTeamId} - {WorkTeamName}", SystemActionType.ServiceDataReplication, team.Id, existingTeam.Name);
        }
    }

    private async Task DeleteWorkTeam(TeamDeletedEvent team, CancellationToken cancellationToken)
    {
        var existingTeam = await _workDbContext.WorkTeams.FirstOrDefaultAsync(t => t.Id == team.Id, cancellationToken);

        try
        {
            if (existingTeam is null)
            {
                _logger.LogInformation("[{SystemActionType}] Work Team deleted. Unable to find work team {WorkTeamId} to delete.", SystemActionType.ServiceDataReplication, team.Id);
            }
            else
            {
                // TODO: consider making the team inactive or archiving it instead of deleting it.  Maybe we only delete if the Work team has never been used?

                _workDbContext.WorkTeams.Remove(existingTeam);
                await _workDbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("[{SystemActionType}] Work Team deleted. {WorkTeamId} - {WorkTeamName}", SystemActionType.ServiceDataReplication, existingTeam.Id, existingTeam.Name);
            }

        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "[{SystemActionType}] Work Team delete action failed to save. {WorkTeamId} - {WorkTeamName}", SystemActionType.ServiceDataReplication, team.Id, existingTeam?.Name ?? "Unknown Team");
        }
    }
}
