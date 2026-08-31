using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A deployment environment was defined.
/// </summary>
public sealed record EnvironmentAddedEvent : DomainEvent, IProductManagementEvent
{
    public EnvironmentAddedEvent(Guid id, int key, string name, EnvironmentCategory category, int ringOrder, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        Name = name;
        Category = category;
        RingOrder = ringOrder;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public EnvironmentCategory Category { get; }
    public int RingOrder { get; }
}
