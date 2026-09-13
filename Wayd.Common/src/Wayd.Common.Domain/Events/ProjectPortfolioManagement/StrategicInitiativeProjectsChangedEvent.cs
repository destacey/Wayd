using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// Projects were linked to or unlinked from a strategic initiative.
/// </summary>
/// <remarks>
/// <see cref="Added"/> and <see cref="Removed"/> are the change; <see cref="ProjectIds"/> is the set
/// afterwards, for a consumer keeping a copy.
/// </remarks>
public sealed record StrategicInitiativeProjectsChangedEvent : DomainEvent<StrategicInitiativeProjectsChangedEvent>, IDomainEventDescriptor, IPpmEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public StrategicInitiativeProjectsChangedEvent(
        Guid id,
        int key,
        Guid[] added,
        Guid[] removed,
        Guid[] projectIds,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Added = [.. added];
        Removed = [.. removed];
        ProjectIds = [.. projectIds];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid[] Added { get; }
    public Guid[] Removed { get; }
    public Guid[] ProjectIds { get; }

    [JsonIgnore]
    public string AggregateType => "StrategicInitiative";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
