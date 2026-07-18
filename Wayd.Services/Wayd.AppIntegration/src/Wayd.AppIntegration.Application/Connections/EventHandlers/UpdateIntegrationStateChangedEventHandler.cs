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
            var connections = await _appIntegrationDbContext.AzureDevOpsBoardsConnections.ToListAsync(cancellationToken);
            foreach (var connection in connections)
            {
                var workProcess = connection.Configuration.WorkProcesses.FirstOrDefault(p => p.HasIntegration && p.IntegrationState!.InternalId == @event.IntegrationState.InternalId);
                if (workProcess is not null)
                {
                    workProcess.UpdateIntegrationState(@event.IntegrationState.IsActive);

                    using (LogContext.PushProperty("EventPayload", @event))
                    {
                        _logger.LogInformation("Event processed for {EventHandler}", nameof(UpdateIntegrationStateChangedEventHandler));
                    }

                    break;
                }
            }

            await _appIntegrationDbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
