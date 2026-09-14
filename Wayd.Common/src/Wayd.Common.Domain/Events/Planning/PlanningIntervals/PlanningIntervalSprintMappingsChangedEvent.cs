using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.PlanningIntervals;

/// <summary>
/// Team sprints were mapped to, or unmapped from, a planning interval's iterations.
/// </summary>
/// <remarks>
/// Moving a sprint to another iteration is recorded as its old mapping removed and its new one added.
/// </remarks>
public sealed record PlanningIntervalSprintMappingsChangedEvent : DomainEvent<PlanningIntervalSprintMappingsChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public PlanningIntervalSprintMappingsChangedEvent(
        Guid id,
        int key,
        PlanningIntervalSprintMapping[] added,
        PlanningIntervalSprintMapping[] removed,
        PlanningIntervalSprintMapping[] sprintMappings,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Added = [.. added];
        Removed = [.. removed];
        SprintMappings = [.. sprintMappings];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public PlanningIntervalSprintMapping[] Added { get; }
    public PlanningIntervalSprintMapping[] Removed { get; }

    /// <summary>Every sprint mapping in the interval after the change.</summary>
    public PlanningIntervalSprintMapping[] SprintMappings { get; }

    [JsonIgnore]
    public string AggregateType => "PlanningInterval";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
