using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.Runtime.Routing;

namespace Wayd.Infrastructure.Common.Services;

public class EventPublisher : IEventPublisher
{
    private readonly ILogger<EventPublisher> _logger;
    private readonly IMessageBus _bus;

    // Per-event-type cache of "does this event have a handler". Populated on first publish so the
    // no-handler probe (an IndeterminateRoutesException) is paid at most once per event type, not on
    // every publish of a subscriber-less event.
    private static readonly ConcurrentDictionary<Type, bool> HasHandler = new();

    public EventPublisher(ILogger<EventPublisher> logger, IMessageBus bus) =>
        (_logger, _bus) = (logger, bus);

    public async Task PublishAsync(IEvent @event)
    {
        _logger.LogInformation("Publishing Event : {event}", @event.GetType().Name);

        var eventType = @event.GetType();

        // Events are dispatched INLINE via InvokeAsync: the handler runs synchronously in this call
        // before it returns, preserving read-your-writes for the cross-domain replication projections
        // (same-Id copies) that in-request reads and subsequent commands depend on. PublishAsync would
        // instead enqueue to Wolverine's buffered local queue and run the handler later on a background
        // thread — that is Stage C (selective async routing), not this phase.
        //
        // InvokeAsync throws IndeterminateRoutesException when a message type has no handler, whereas
        // MediatR's Publish was a silent no-op for events with no subscriber. Some domain events are
        // raised with no handler today (e.g. the Program* events), so treat "no handler" as a no-op and
        // remember it per type. Wolverine routes on the runtime type, so passing the event via its
        // IEvent variable still dispatches to the concrete event type's handler.
        if (HasHandler.TryGetValue(eventType, out var handled) && !handled)
        {
            return;
        }

        try
        {
            await _bus.InvokeAsync(@event);
            HasHandler.TryAdd(eventType, true);
        }
        catch (IndeterminateRoutesException)
        {
            // No handler for this event type — matches MediatR's no-subscriber no-op. Cache so we don't
            // pay the exception again for this type.
            HasHandler[eventType] = false;
        }
    }
}
