using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// The strategic themes a program is tagged with changed.
/// </summary>
/// <remarks>
/// Carries the full set after the change rather than what was added or removed, matching
/// <see cref="ProgramCreatedEvent.StrategicThemes"/> so the two compare directly.
/// </remarks>
public sealed record ProgramStrategicThemesChangedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProgramStrategicThemesChangedEvent(
        Guid id,
        int key,
        Guid[] strategicThemes,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        StrategicThemes = [.. strategicThemes];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The strategic theme ids the program carries after the change.</summary>
    public Guid[] StrategicThemes { get; }

    [JsonIgnore]
    public string AggregateType => "Program";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
