using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// An environment was renamed or moved in the rollout order.
/// </summary>
/// <remarks>
/// The category is not part of it: reclassifying changes what past deployments count toward, so it is
/// <see cref="EnvironmentReclassifiedEventV2"/>.
/// </remarks>
public sealed record EnvironmentDetailsUpdatedEvent : DomainEvent<EnvironmentDetailsUpdatedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public EnvironmentDetailsUpdatedEvent(Guid id, int key, string name, int ringOrder, EnvironmentDetails? previous, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        RingOrder = ringOrder;
        Previous = previous;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public int RingOrder { get; }

    /// <summary>
    /// The details this edit replaced. Grouped so that null can only mean "not recorded", as
    /// <c>ProjectDetailsUpdatedEvent.Previous</c> is.
    /// </summary>
    public EnvironmentDetails? Previous { get; }

    [JsonIgnore]
    public string AggregateType => "DeploymentEnvironment";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
