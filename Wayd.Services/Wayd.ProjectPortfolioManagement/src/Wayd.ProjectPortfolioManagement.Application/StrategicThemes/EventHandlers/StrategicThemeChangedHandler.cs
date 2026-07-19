using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events.StrategicManagement;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicThemes.EventHandlers;

/// <summary>
/// Replicates StrategicManagement <c>StrategicTheme</c> changes into the PPM <c>PpmStrategicThemes</c>
/// projection (same Id).
/// </summary>
/// <remarks>
/// The <c>StrategicTheme*</c> events are delivered durably (see <c>DurableEventRoutes</c>): enlisted in the
/// Wolverine outbox, delivered on a background thread, and governed by a retry-with-cooldown → dead-letter
/// failure policy. So this handler lets exceptions propagate — a transient DB failure is retried by Wolverine
/// and a poison message dead-letters. The per-message idempotency guards (create no-ops if the row already
/// exists; update/delete no-op if it does not) make redelivery safe.
/// </remarks>
public sealed class StrategicThemeChangedHandler(IProjectPortfolioManagementDbContext ppmContext, ILogger<StrategicThemeChangedHandler> logger)
{
    private readonly IProjectPortfolioManagementDbContext _ppmContext = ppmContext;
    private readonly ILogger<StrategicThemeChangedHandler> _logger = logger;

    public async Task Handle(StrategicThemeCreatedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling PPM {SystemActionType} for a new Strategic Theme {StrategicThemeId}.", SystemActionType.ServiceDataReplication, @event.Id);
        await CreateStrategicTheme(@event, cancellationToken);
    }

    public async Task Handle(StrategicThemeUpdatedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling PPM {SystemActionType} for an updated Strategic Theme {StrategicThemeId}.", SystemActionType.ServiceDataReplication, @event.Id);
        await UpdateStrategicTheme(@event, cancellationToken);
    }

    public async Task Handle(StrategicThemeDeletedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling PPM {SystemActionType} for a deleted Strategic Theme {StrategicThemeId}.", SystemActionType.ServiceDataReplication, @event.Id);
        await DeleteStrategicTheme(@event, cancellationToken);
    }

    private async Task CreateStrategicTheme(StrategicThemeCreatedEvent createdEvent, CancellationToken cancellationToken)
    {
        // Idempotency guard, not error handling: a redelivery or a race with the Hangfire SyncStrategicThemes
        // bulk path may find the projection already present — that is a success, not a fault.
        if (await _ppmContext.PpmStrategicThemes.AnyAsync(x => x.Id == createdEvent.Id, cancellationToken))
        {
            _logger.LogInformation("PPM {SystemActionType} for a new Strategic Theme skipped: Strategic Theme {StrategicThemeId} already exists in the PPM system.", SystemActionType.ServiceDataReplication, createdEvent.Id);
            return;
        }

        var theme = new StrategicTheme(createdEvent);
        await _ppmContext.PpmStrategicThemes.AddAsync(theme, cancellationToken);
        await _ppmContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful PPM {SystemActionType} for the Strategic Theme {StrategicThemeId} created action.", SystemActionType.ServiceDataReplication, createdEvent.Id);
    }

    private async Task UpdateStrategicTheme(StrategicThemeUpdatedEvent updatedEvent, CancellationToken cancellationToken)
    {
        var existingStrategicTheme = await _ppmContext.PpmStrategicThemes
            .FirstOrDefaultAsync(x => x.Id == updatedEvent.Id, cancellationToken);
        if (existingStrategicTheme is null)
        {
            // The create event has not been applied yet (out-of-order delivery) or the theme was deleted.
            // No-op rather than throw: a create redelivery or the Hangfire bulk sync will converge the state.
            _logger.LogWarning("PPM {SystemActionType} for an updated Strategic Theme skipped: Strategic Theme {StrategicThemeId} does not exist in the PPM system.", SystemActionType.ServiceDataReplication, updatedEvent.Id);
            return;
        }

        existingStrategicTheme.Update(updatedEvent.Name, updatedEvent.Description, updatedEvent.State);
        await _ppmContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful PPM {SystemActionType} for the Strategic Theme {StrategicThemeId} update action.", SystemActionType.ServiceDataReplication, updatedEvent.Id);
    }

    private async Task DeleteStrategicTheme(StrategicThemeDeletedEvent deletedEvent, CancellationToken cancellationToken)
    {
        var existingStrategicTheme = await _ppmContext.PpmStrategicThemes
            .FirstOrDefaultAsync(x => x.Id == deletedEvent.Id, cancellationToken);
        if (existingStrategicTheme is null)
        {
            // Already gone (redelivery, or the theme never replicated) — deleting nothing is the goal state.
            _logger.LogInformation("PPM {SystemActionType} for a deleted Strategic Theme skipped: Strategic Theme {StrategicThemeId} does not exist in the PPM system.", SystemActionType.ServiceDataReplication, deletedEvent.Id);
            return;
        }

        _ppmContext.PpmStrategicThemes.Remove(existingStrategicTheme);
        await _ppmContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful PPM {SystemActionType} for the Strategic Theme {StrategicThemeId} delete action.", SystemActionType.ServiceDataReplication, deletedEvent.Id);
    }
}
