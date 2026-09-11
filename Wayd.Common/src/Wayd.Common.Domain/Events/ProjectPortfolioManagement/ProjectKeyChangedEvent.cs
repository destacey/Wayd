using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A project's key changed, cascading to the key of every task under it.
/// </summary>
/// <remarks>
/// Frozen at its published shape and never raised; <see cref="ProjectKeyChangedEventV2"/> replaced it. Kept
/// so every payload written as this type still deserializes into it — its name and members are the contract
/// those payloads were written against, so neither may change.
/// </remarks>
[Obsolete("Superseded by ProjectKeyChangedEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record ProjectKeyChangedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectKeyChangedEvent(
        Guid id,
        ProjectKey key,
        string name,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;

        Timestamp = timestamp;
    }

    public Guid Id { get; }

    /// <summary>The project's key after the change.</summary>
    public ProjectKey Key { get; }

    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
