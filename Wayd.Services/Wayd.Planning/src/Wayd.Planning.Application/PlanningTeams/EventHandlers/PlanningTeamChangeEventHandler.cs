using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events.Organization;

namespace Wayd.Planning.Application.PlanningTeams.EventHandlers;

/// <summary>
/// Replicates Organization <c>Team</c> changes into the Planning domain's <c>PlanningTeam</c> projection
/// (same Id).
/// </summary>
/// <remarks>
/// The <c>Team*</c> events are delivered durably (see <c>DurableEventRoutes</c>): enlisted in the Wolverine
/// outbox, delivered on a background thread, and governed by a retry-with-cooldown → dead-letter failure
/// policy. So this handler lets exceptions propagate — a transient DB failure is retried by Wolverine and a
/// poison message dead-letters. The per-message idempotency guards (create no-ops if the row already exists;
/// update/activate/deactivate/delete no-op if it does not) make redelivery safe.
/// <para>
/// Because delivery is asynchronous, a <c>PlanningTeam</c> may not exist at the instant a follow-up command
/// references it. <c>ManagePlanningIntervalTeamsCommand</c> validates team existence and fails cleanly rather
/// than FK-faulting, which is what makes async replication safe for this projection.
/// </para>
/// </remarks>
public sealed class PlanningTeamChangeEventHandler
{
    private readonly IPlanningDbContext _planningDbContext;
    private readonly ILogger<PlanningTeamChangeEventHandler> _logger;

    public PlanningTeamChangeEventHandler(IPlanningDbContext planningDbContext, ILogger<PlanningTeamChangeEventHandler> logger)
    {
        _planningDbContext = planningDbContext;
        _logger = logger;
    }

    public async Task Handle(TeamCreatedEvent @event, CancellationToken cancellationToken)
    {
        await CreatePlanningTeam(@event, cancellationToken);
    }

    public async Task Handle(TeamUpdatedEvent @event, CancellationToken cancellationToken)
    {
        await UpdatePlanningTeam(@event, cancellationToken);
    }

    public async Task Handle(TeamActivatedEvent @event, CancellationToken cancellationToken)
    {
        await ActivatePlanningTeam(@event, cancellationToken);
    }

    public async Task Handle(TeamDeactivatedEvent @event, CancellationToken cancellationToken)
    {
        await DeactivatePlanningTeam(@event, cancellationToken);
    }

    public async Task Handle(TeamDeletedEvent @event, CancellationToken cancellationToken)
    {
        await DeletePlanningTeam(@event, cancellationToken);
    }

    private async Task CreatePlanningTeam(TeamCreatedEvent team, CancellationToken cancellationToken)
    {
        if (await _planningDbContext.PlanningTeams.AnyAsync(t => t.Id == team.Id, cancellationToken))
        {
            _logger.LogInformation("[{SystemActionType}] Planning Team create action skipped. Team already exists. {PlanningTeamId} - {PlanningTeamName}", SystemActionType.ServiceDataReplication, team.Id, team.Name);
            return;
        }

        var planningTeam = new PlanningTeam(team);
        await _planningDbContext.PlanningTeams.AddAsync(planningTeam, cancellationToken);
        await _planningDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Planning Team created. {PlanningTeamId} - {PlanningTeamName}", SystemActionType.ServiceDataReplication, team.Id, team.Name);
    }

    private async Task UpdatePlanningTeam(TeamUpdatedEvent team, CancellationToken cancellationToken)
    {
        var existingTeam = await _planningDbContext.PlanningTeams.FirstOrDefaultAsync(t => t.Id == team.Id, cancellationToken);
        if (existingTeam is null)
        {
            _logger.LogInformation("[{SystemActionType}] Planning Team update action skipped. Unable to find team {PlanningTeamId} to update.", SystemActionType.ServiceDataReplication, team.Id);
            return;
        }

        existingTeam.Update(team);
        await _planningDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Planning Team updated. {PlanningTeamId} - {PlanningTeamName}", SystemActionType.ServiceDataReplication, team.Id, team.Name);
    }

    private async Task ActivatePlanningTeam(TeamActivatedEvent team, CancellationToken cancellationToken)
    {
        var existingTeam = await _planningDbContext.PlanningTeams.FirstOrDefaultAsync(t => t.Id == team.Id, cancellationToken);
        if (existingTeam is null)
        {
            _logger.LogInformation("[{SystemActionType}] Planning Team activate action skipped. Unable to find team {PlanningTeamId} to activate.", SystemActionType.ServiceDataReplication, team.Id);
            return;
        }

        existingTeam.UpdateIsActive(true);
        await _planningDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Planning Team activated. {PlanningTeamId} - {PlanningTeamName}", SystemActionType.ServiceDataReplication, team.Id, existingTeam.Name);
    }

    private async Task DeactivatePlanningTeam(TeamDeactivatedEvent team, CancellationToken cancellationToken)
    {
        var existingTeam = await _planningDbContext.PlanningTeams.FirstOrDefaultAsync(t => t.Id == team.Id, cancellationToken);
        if (existingTeam is null)
        {
            _logger.LogInformation("[{SystemActionType}] Planning Team deactivate action skipped. Unable to find team {PlanningTeamId} to deactivate.", SystemActionType.ServiceDataReplication, team.Id);
            return;
        }

        existingTeam.UpdateIsActive(false);
        await _planningDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Planning Team deactivated. {PlanningTeamId} - {PlanningTeamName}", SystemActionType.ServiceDataReplication, team.Id, existingTeam.Name);
    }

    private async Task DeletePlanningTeam(TeamDeletedEvent team, CancellationToken cancellationToken)
    {
        var existingTeam = await _planningDbContext.PlanningTeams.FirstOrDefaultAsync(t => t.Id == team.Id, cancellationToken);
        if (existingTeam is null)
        {
            _logger.LogInformation("[{SystemActionType}] Planning Team delete action skipped. Unable to find team {PlanningTeamId} to delete.", SystemActionType.ServiceDataReplication, team.Id);
            return;
        }

        // TODO: consider making the team inactive or archiving it instead of deleting it.  Maybe we only delete if the planning team has never been used?
        _planningDbContext.PlanningTeams.Remove(existingTeam);
        await _planningDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[{SystemActionType}] Planning Team deleted. {PlanningTeamId} - {PlanningTeamName}", SystemActionType.ServiceDataReplication, existingTeam.Id, existingTeam.Name);
    }
}
