using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// An environment's category changed.
/// </summary>
/// <remarks>
/// Frozen at its published shape and never raised; <see cref="EnvironmentReclassifiedEventV2"/> replaced it.
/// Kept so every payload written as this type still deserializes into it — its name and members are the
/// contract those payloads were written against, so neither may change.
/// </remarks>
[Obsolete("Superseded by EnvironmentReclassifiedEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record EnvironmentReclassifiedEvent : DomainEvent, IProductManagementEvent
{
    [JsonConstructor]
    public EnvironmentReclassifiedEvent(Guid id, int key, string name, EnvironmentCategory fromCategory, EnvironmentCategory toCategory, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
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

    [JsonIgnore]
    public string AggregateType => "DeploymentEnvironment";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
