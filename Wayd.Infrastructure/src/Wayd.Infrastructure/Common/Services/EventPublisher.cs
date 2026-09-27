using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.Runtime;

namespace Wayd.Infrastructure.Common.Services;

public class EventPublisher : IEventPublisher
{
    private readonly ILogger<EventPublisher> _logger;
    private readonly IMessageBus _bus;
    private readonly WolverineRuntime _runtime;

    public EventPublisher(ILogger<EventPublisher> logger, IMessageBus bus, IWolverineRuntime runtime) =>
        (_logger, _bus, _runtime) = (logger, bus, (WolverineRuntime)runtime);

    public async Task PublishAsync(IEvent @event)
    {
        _logger.LogInformation("Publishing Event : {event}", @event.GetType().Name);

        // This is the INLINE dispatch path (durable events instead enlist in the outbox in BaseDbContext).
        // InvokeAsync runs the handler synchronously before returning, preserving read-your-writes for the
        // cross-domain replication projections (same-Id copies) that in-request reads and subsequent commands
        // depend on.
        //
        // InvokeAsync demands a handler: for a type with none, Wolverine logs an error and throws. Many domain
        // events are raised with no handler and are only recorded in the activity log, so ask the handler
        // graph first. Wolverine routes on the runtime type, so passing the event via its IEvent variable
        // still dispatches to the concrete event type's handler.
        if (_runtime.Handlers.ChainFor(@event.GetType()) is null)
        {
            return;
        }

        await _bus.InvokeAsync(@event);
    }
}
