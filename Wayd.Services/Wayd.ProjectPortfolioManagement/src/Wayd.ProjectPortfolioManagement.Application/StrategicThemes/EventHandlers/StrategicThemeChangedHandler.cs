using Wayd.Common.Application.Requests.StrategicManagement;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Events.StrategicManagement;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicThemes.EventHandlers;

/// <summary>
/// Keeps the PPM module's <c>PpmStrategicThemes</c> copy of each StrategicManagement theme (same Id).
/// </summary>
/// <remarks>
/// The <c>StrategicTheme*</c> events are delivered durably (see <c>DurableEventRoutes</c>): at least once and
/// in no guaranteed order, with a retry-with-cooldown → dead-letter failure policy. This handler follows the
/// rules in "Consuming an event" (docs/contributing/domain-events.mdx): a change older than the one the copy
/// already holds is skipped, a missing copy is created from the theme's current state, and a theme that no
/// longer exists is left without a copy.
/// </remarks>
public sealed class StrategicThemeChangedHandler(
    IProjectPortfolioManagementDbContext ppmContext,
    IDispatcher dispatcher,
    ILogger<StrategicThemeChangedHandler> logger)
{
    private readonly IProjectPortfolioManagementDbContext _ppmContext = ppmContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ILogger<StrategicThemeChangedHandler> _logger = logger;

    public async Task Handle(StrategicThemeCreatedEvent @event, CancellationToken cancellationToken)
    {
        if (await _ppmContext.PpmStrategicThemes.AnyAsync(x => x.Id == @event.Id, cancellationToken))
        {
            _logger.LogInformation("PPM {SystemActionType} for a new Strategic Theme skipped: Strategic Theme {StrategicThemeId} already has a copy.", SystemActionType.ServiceDataReplication, @event.Id);
            return;
        }

        await CreateFromSource(@event.Id, @event.Timestamp, cancellationToken);
    }

    // The payload's State is the state the theme happened to be in, not part of the change: an update only
    // renames or redescribes, and every transition raises its own event.
    public async Task Handle(StrategicThemeUpdatedEvent @event, CancellationToken cancellationToken)
    {
        await Apply(@event.Id, @event.Timestamp, t => t.ApplyDetails(@event.Name, @event.Description, @event.Timestamp), "details", cancellationToken);
    }

    public async Task Handle(StrategicThemeActivatedEvent @event, CancellationToken cancellationToken)
    {
        await Apply(@event.Id, @event.Timestamp, t => t.ApplyState(StrategicThemeState.Active, @event.Timestamp), "activation", cancellationToken);
    }

    public async Task Handle(StrategicThemeArchivedEvent @event, CancellationToken cancellationToken)
    {
        await Apply(@event.Id, @event.Timestamp, t => t.ApplyState(StrategicThemeState.Archived, @event.Timestamp), "archive", cancellationToken);
    }

    public async Task Handle(StrategicThemeDeletedEvent @event, CancellationToken cancellationToken)
    {
        var existingStrategicTheme = await _ppmContext.PpmStrategicThemes
            .FirstOrDefaultAsync(x => x.Id == @event.Id, cancellationToken);
        if (existingStrategicTheme is null)
        {
            _logger.LogInformation("PPM {SystemActionType} for a deleted Strategic Theme skipped: Strategic Theme {StrategicThemeId} has no copy.", SystemActionType.ServiceDataReplication, @event.Id);
            return;
        }

        _ppmContext.PpmStrategicThemes.Remove(existingStrategicTheme);
        await _ppmContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful PPM {SystemActionType} for the Strategic Theme {StrategicThemeId} delete action.", SystemActionType.ServiceDataReplication, @event.Id);
    }

    private async Task Apply(Guid themeId, Instant timestamp, Func<StrategicTheme, bool> apply, string change, CancellationToken cancellationToken)
    {
        var existingStrategicTheme = await _ppmContext.PpmStrategicThemes
            .FirstOrDefaultAsync(x => x.Id == themeId, cancellationToken);
        if (existingStrategicTheme is null)
        {
            await CreateFromSource(themeId, timestamp, cancellationToken);
            return;
        }

        if (!apply(existingStrategicTheme))
        {
            _logger.LogInformation("PPM {SystemActionType} for a Strategic Theme {Change} skipped: Strategic Theme {StrategicThemeId} already holds this or a newer change.", SystemActionType.ServiceDataReplication, change, themeId);
            return;
        }

        await _ppmContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful PPM {SystemActionType} for the Strategic Theme {StrategicThemeId} {Change}.", SystemActionType.ServiceDataReplication, themeId, change);
    }

    private async Task CreateFromSource(Guid themeId, Instant timestamp, CancellationToken cancellationToken)
    {
        var source = await _dispatcher.Send(new GetStrategicThemeDataQuery(themeId), cancellationToken);
        if (source is null)
        {
            _logger.LogInformation("PPM {SystemActionType} copy not created: Strategic Theme {StrategicThemeId} no longer exists.", SystemActionType.ServiceDataReplication, themeId);
            return;
        }

        await _ppmContext.PpmStrategicThemes.AddAsync(new StrategicTheme(source, timestamp), cancellationToken);
        await _ppmContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful PPM {SystemActionType} creating Strategic Theme {StrategicThemeId} from its source.", SystemActionType.ServiceDataReplication, themeId);
    }
}
