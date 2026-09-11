using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A project's role assignments changed.
/// </summary>
/// <remarks>
/// Frozen at its published shape and never raised; <see cref="ProjectRolesChangedEventV2"/> replaced it. Kept
/// so every payload written as this type still deserializes into it — its name and members are the contract
/// those payloads were written against, so neither may change.
/// </remarks>
[Obsolete("Superseded by ProjectRolesChangedEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record ProjectRolesChangedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectRolesChangedEvent(
        Guid id,
        ProjectKey key,
        string name,
        Dictionary<int, Guid[]>? roles,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Roles = roles?.ToDictionary(x => x.Key, x => x.Value.ToArray());

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }
    public string Name { get; }

    /// <summary>
    /// Every role assignment on the project after the change. The key is the role type id, the value the
    /// employees holding it.
    /// </summary>
    public Dictionary<int, Guid[]>? Roles { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
