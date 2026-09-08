using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// The strategic themes a project is tagged with changed.
/// </summary>
/// <remarks>
/// Carries the full set after the change rather than what was added or removed, matching
/// <see cref="ProjectCreatedEvent.StrategicThemes"/> so the two compare directly.
/// </remarks>
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
        : base(actor)
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
