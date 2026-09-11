using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// The strategic themes a program is tagged with changed.
/// </summary>
/// <remarks>
/// Carries both the change and its result, like the roles events. <see cref="Added"/> and
/// <see cref="Removed"/> are the fact, for a consumer that reacts to it. <see cref="StrategicThemes"/>
/// is the full set afterwards, matching <see cref="ProgramCreatedEvent.StrategicThemes"/>, for a consumer that
/// keeps a copy: applying the latest set is correct however deliveries were ordered or repeated, and
/// applying the deltas is not.
/// </remarks>
public sealed record ProgramStrategicThemesChangedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProgramStrategicThemesChangedEvent(
        Guid id,
        int key,
        Guid[] added,
        Guid[] removed,
        Guid[] strategicThemes,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Added = [.. added];
        Removed = [.. removed];
        StrategicThemes = [.. strategicThemes];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The strategic theme ids this change tagged the program with.</summary>
    public Guid[] Added { get; }

    /// <summary>The strategic theme ids this change removed.</summary>
    public Guid[] Removed { get; }

    /// <summary>The strategic theme ids the program carries after the change.</summary>
    public Guid[] StrategicThemes { get; }

    [JsonIgnore]
    public string AggregateType => "Program";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
