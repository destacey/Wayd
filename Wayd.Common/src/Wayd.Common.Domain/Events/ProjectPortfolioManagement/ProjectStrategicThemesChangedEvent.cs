using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// The strategic themes a project is tagged with changed.
/// </summary>
/// <remarks>
/// Frozen at its published shape and never raised; <see cref="ProjectStrategicThemesChangedEventV2"/>
/// replaced it. Kept so every payload written as this type still deserializes into it — its name and members
/// are the contract those payloads were written against, so neither may change.
/// </remarks>
[Obsolete("Superseded by ProjectStrategicThemesChangedEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record ProjectStrategicThemesChangedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectStrategicThemesChangedEvent(
        Guid id,
        ProjectKey key,
        string name,
        Guid[] strategicThemes,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        StrategicThemes = [.. strategicThemes];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }
    public string Name { get; }

    /// <summary>The strategic theme ids the project carries after the change.</summary>
    public Guid[] StrategicThemes { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
