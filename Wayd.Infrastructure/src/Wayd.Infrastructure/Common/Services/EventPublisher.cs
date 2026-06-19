using MediatR;
using Microsoft.Extensions.Logging;

namespace Wayd.Infrastructure.Common.Services;

public class EventPublisher : IEventPublisher
{
    private readonly ILogger<EventPublisher> _logger;
    private readonly IPublisher _mediator;

    public EventPublisher(ILogger<EventPublisher> logger, IPublisher mediator) =>
        (_logger, _mediator) = (logger, mediator);

    public Task PublishAsync(IEvent @event)
    {
        if (@event.GetType().GetInterfaces().Any(i => i == typeof(IGenericDomainEvent)))
        {
            _logger.LogInformation("Publishing Event : {event}, {entity}", @event.GetType().Name, @event.GetType().GenericTypeArguments.FirstOrDefault()?.Name);
        }
        else
        {
            _logger.LogInformation("Publishing Event : {event}", @event.GetType().Name);
        }
        return _mediator.Publish(CreateEventNotification(@event));
    }

    private INotification CreateEventNotification(IEvent @event) =>
        (INotification)Activator.CreateInstance(
            typeof(EventNotification<>).MakeGenericType(@event.GetType()), @event)!;
}