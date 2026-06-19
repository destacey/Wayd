namespace Wayd.Common.Application.Events;

public interface IEventPublisher : ITransientService
{
    Task PublishAsync(IEvent @event);
}