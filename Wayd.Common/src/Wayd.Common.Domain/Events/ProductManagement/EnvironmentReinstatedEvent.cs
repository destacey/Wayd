using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A retired environment was put back into use and can be deployed into again.
/// </summary>
/// <remarks>
/// The reverse of <see cref="EnvironmentRetiredEventV2"/>. Only a retired environment can be reactivated,
/// so the type records both ends of the transition.
/// </remarks>
public sealed record EnvironmentReinstatedEvent : DomainEvent<EnvironmentReinstatedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    [JsonConstructor]
    public EnvironmentReinstatedEvent(Guid id, int key, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    [JsonIgnore]
    public string AggregateType => "DeploymentEnvironment";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
