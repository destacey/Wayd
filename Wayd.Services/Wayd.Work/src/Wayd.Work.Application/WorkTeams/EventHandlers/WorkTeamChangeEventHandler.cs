using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkTeams.EventHandlers;

/// <summary>
/// Replicates Organization <c>Team</c> changes into the Work domain's <c>WorkTeam</c> projection (same Id).
/// </summary>
/// <remarks>
/// The <c>Team*</c> events are delivered durably (see <c>DurableEventRoutes</c>): enlisted in the Wolverine
/// outbox, delivered on a background thread, and governed by a retry-with-cooldown → dead-letter failure
/// policy. So this handler lets exceptions propagate — a transient DB failure is retried by Wolverine and a
/// poison message dead-letters. The per-message idempotency guards (create no-ops if the row already exists;
/// update/activate/deactivate/delete no-op if it does not) make redelivery safe.
/// </remarks>
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

        var workTeam = new WorkTeam(team);
        await _workDbContext.WorkTeams.AddAsync(workTeam, cancellationToken);
        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Work Team created. {WorkTeamId} - {WorkTeamName}", SystemActionType.ServiceDataReplication, team.Id, team.Name);
    }

    private async Task UpdateWorkTeam(TeamUpdatedEvent team, CancellationToken cancellationToken)
    {
        var existingTeam = await _workDbContext.WorkTeams.FirstOrDefaultAsync(t => t.Id == team.Id, cancellationToken);
        if (existingTeam is null)
        {
            _logger.LogInformation("[{SystemActionType}] Work Team update action skipped. Unable to find work team {WorkTeamId} to update.", SystemActionType.ServiceDataReplication, team.Id);
            return;
        }

        existingTeam.Update(team);
        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Work Team updated. {WorkTeamId} - {WorkTeamName}", SystemActionType.ServiceDataReplication, team.Id, team.Name);
    }

    private async Task ActivateWorkTeam(TeamActivatedEvent team, CancellationToken cancellationToken)
    {
        var existingTeam = await _workDbContext.WorkTeams.FirstOrDefaultAsync(t => t.Id == team.Id, cancellationToken);
        if (existingTeam is null)
        {
            _logger.LogInformation("[{SystemActionType}] Work Team activate action skipped. Unable to find work team {WorkTeamId} to activate.", SystemActionType.ServiceDataReplication, team.Id);
            return;
        }

        existingTeam.UpdateIsActive(true);
        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Work Team activated. {WorkTeamId} - {WorkTeamName}", SystemActionType.ServiceDataReplication, team.Id, existingTeam.Name);
    }

    private async Task DeactivateWorkTeam(TeamDeactivatedEvent team, CancellationToken cancellationToken)
    {
        var existingTeam = await _workDbContext.WorkTeams.FirstOrDefaultAsync(t => t.Id == team.Id, cancellationToken);
        if (existingTeam is null)
        {
            _logger.LogInformation("[{SystemActionType}] Work Team deactivate action skipped. Unable to find work team {WorkTeamId} to deactivate.", SystemActionType.ServiceDataReplication, team.Id);
            return;
        }

        existingTeam.UpdateIsActive(false);
        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Work Team deactivated. {WorkTeamId} - {WorkTeamName}", SystemActionType.ServiceDataReplication, team.Id, existingTeam.Name);
    }

    private async Task DeleteWorkTeam(TeamDeletedEvent team, CancellationToken cancellationToken)
    {
        var existingTeam = await _workDbContext.WorkTeams.FirstOrDefaultAsync(t => t.Id == team.Id, cancellationToken);
        if (existingTeam is null)
        {
            _logger.LogInformation("[{SystemActionType}] Work Team delete action skipped. Unable to find work team {WorkTeamId} to delete.", SystemActionType.ServiceDataReplication, team.Id);
            return;
        }

        // TODO: consider making the team inactive or archiving it instead of deleting it.  Maybe we only delete if the Work team has never been used?
        _workDbContext.WorkTeams.Remove(existingTeam);
        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Work Team deleted. {WorkTeamId} - {WorkTeamName}", SystemActionType.ServiceDataReplication, existingTeam.Id, existingTeam.Name);
    }
}
