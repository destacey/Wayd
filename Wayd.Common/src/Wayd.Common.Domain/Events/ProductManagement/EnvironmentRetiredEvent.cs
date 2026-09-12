using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// An environment was retired and can no longer be deployed into.
/// </summary>
/// <remarks>
/// Frozen at its published shape and never raised; <see cref="EnvironmentRetiredEventV2"/> replaced it. Kept
/// so every payload written as this type still deserializes into it — its name and members are the contract
/// those payloads were written against, so neither may change.
/// </remarks>
[Obsolete("Superseded by EnvironmentRetiredEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record EnvironmentRetiredEvent : DomainEvent, IProductManagementEvent
{
    [JsonConstructor]
    public EnvironmentRetiredEvent(Guid id, int key, string name, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "DeploymentEnvironment";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
