using Microsoft.Extensions.Logging;
using Wolverine;

namespace Wayd.Infrastructure.Common.Services;

public class EventPublisher : IEventPublisher
{
    private readonly ILogger<EventPublisher> _logger;
    private readonly IMessageBus _bus;

    public EventPublisher(ILogger<EventPublisher> logger, IMessageBus bus) =>
        (_logger, _bus) = (logger, bus);

    public Task PublishAsync(IEvent @event)
    {
        _logger.LogInformation("Publishing Event : {event}", @event.GetType().Name);

        // Wolverine routes on the runtime type, so passing the event via its IEvent variable still
        // dispatches to handlers registered for the concrete event type. Inline/synchronous for now
        // (no durable outbox) — the handler runs in a fresh DI scope reading just-committed data.
        return _bus.PublishAsync(@event).AsTask();
    }
}
