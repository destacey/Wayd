using Serilog.Context;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events;

namespace Wayd.AppIntegration.Application.Connections.EventHandlers;

public sealed class UpdateIntegrationStateChangedEventHandler(IAppIntegrationDbContext appIntegrationDbContext, ILogger<UpdateIntegrationStateChangedEventHandler> logger)
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly ILogger<UpdateIntegrationStateChangedEventHandler> _logger = logger;

    public async Task Handle(IntegrationStateChangedEvent<Guid> @event, CancellationToken cancellationToken)
    {
        if (@event.SystemContext == SystemContext.WorkWorkProcess)
        {
            // Azure DevOps processes are shared across organizations, so several connections can
            // hold the same Wayd work process; every one of them must see the change.
            var connections = await _appIntegrationDbContext.AzureDevOpsBoardsConnections.ToListAsync(cancellationToken);
            foreach (var connection in connections)
            {
                var workProcess = connection.Configuration.WorkProcesses.FirstOrDefault(p => p.HasIntegration && p.IntegrationState!.InternalId == @event.IntegrationState.InternalId);
                workProcess?.UpdateIntegrationState(@event.IntegrationState.IsActive);
            }

            using (LogContext.PushProperty("EventPayload", @event))
            {
                _logger.LogInformation("Event processed for {EventHandler}", nameof(UpdateIntegrationStateChangedEventHandler));
            }

            await _appIntegrationDbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
