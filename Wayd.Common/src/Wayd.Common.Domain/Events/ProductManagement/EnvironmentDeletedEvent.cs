using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A deployment environment was deleted, with every deployment into it.
/// </summary>
/// <remarks>
/// Each deployment removed with it raises its own <see cref="DeploymentDeletedEvent"/>, so this carries
/// no list of them. The category travels so a consumer holding production-scoped measures knows whether
/// it counted there.
/// </remarks>
public sealed record EnvironmentDeletedEvent : DomainEvent<EnvironmentDeletedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    public EnvironmentDeletedEvent(Guid id, int key, string name, EnvironmentCategory category, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Category = category;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public EnvironmentCategory Category { get; }

    [JsonIgnore]
    public string AggregateType => "DeploymentEnvironment";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
