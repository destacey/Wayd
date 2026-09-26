using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Domain.Events.Settings;

/// <summary>
/// An administrator saved a system settings section with different values.
/// </summary>
public sealed record SystemSettingsSectionValuesChangedEvent : DomainEvent<SystemSettingsSectionValuesChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public SystemSettingsSectionValuesChangedEvent(
        Guid id,
        string key,
        SettingsScope scope,
        int schemaVersion,
        JsonElement previous,
        JsonElement current,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Scope = scope;
        SchemaVersion = schemaVersion;
        Previous = previous;
        Current = current;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public string Key { get; }
    public SettingsScope Scope { get; }

    /// <summary>The section's stored shape that <see cref="Previous"/> and <see cref="Current"/> are written in.</summary>
    public int SchemaVersion { get; }

    /// <summary>
    /// Every value in effect before the save, code defaults included — a section that had never been saved
    /// records its defaults here.
    /// </summary>
    public JsonElement Previous { get; }

    /// <summary>Every value in effect after the save.</summary>
    public JsonElement Current { get; }

    [JsonIgnore]
    public string AggregateType => "SystemSettingsSection";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
