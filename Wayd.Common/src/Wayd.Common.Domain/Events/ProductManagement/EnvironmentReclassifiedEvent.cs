using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// An environment's category changed.
/// </summary>
/// <remarks>
/// This looks like ordinary configuration and is not. Marking an environment as production
/// retroactively changes deployment frequency and every measure scoped to production — a number
/// somebody reported last week can move without any deployment having happened. That makes it a fact
/// worth a name and its own event, rather than a field on a generic update.
/// </remarks>
public sealed record EnvironmentReclassifiedEvent : DomainEvent, IProductManagementEvent
{
    public EnvironmentReclassifiedEvent(Guid id, int key, string name, EnvironmentCategory fromCategory, EnvironmentCategory toCategory, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        Name = name;
        FromCategory = fromCategory;
        ToCategory = toCategory;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public EnvironmentCategory FromCategory { get; }
    public EnvironmentCategory ToCategory { get; }
}
