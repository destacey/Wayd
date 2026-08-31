using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// An environment was retired and can no longer be deployed into.
/// </summary>
/// <remarks>
/// Retired rather than deleted, because historical deployments still point at it and "what was running
/// in production on this date" has to keep resolving after an environment is decommissioned.
/// </remarks>
public sealed record EnvironmentRetiredEvent : DomainEvent, IProductManagementEvent
{
    public EnvironmentRetiredEvent(Guid id, int key, string name, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        Name = name;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
}
