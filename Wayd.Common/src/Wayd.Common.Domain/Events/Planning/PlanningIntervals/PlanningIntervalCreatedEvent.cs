using System.Text.Json.Serialization;
using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.PlanningIntervals;

/// <summary>
/// A planning interval was created, with the iterations generated for it.
/// </summary>
/// <remarks>
/// A new interval has no teams, no sprint mappings and unlocked objectives, which each have their own event.
/// They are carried anyway because <see cref="PlanningIntervalBaselinedEvent"/> shares this shape and has to
/// describe an interval that has all of them.
/// </remarks>
public sealed record PlanningIntervalCreatedEvent : DomainEvent<PlanningIntervalCreatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    [JsonConstructor]
    public PlanningIntervalCreatedEvent(
        Guid id,
        int key,
        string name,
        string? description,
        LocalDateRange dateRange,
        bool objectivesLocked,
        PlanningIntervalIterationValues[] iterations,
        Guid[] teamIds,
        PlanningIntervalSprintMapping[] sprintMappings,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        DateRange = dateRange;
        ObjectivesLocked = objectivesLocked;
        Iterations = [.. iterations];
        TeamIds = [.. teamIds];
        SprintMappings = [.. sprintMappings];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string? Description { get; }
    public LocalDateRange DateRange { get; }
    public bool ObjectivesLocked { get; }
    public PlanningIntervalIterationValues[] Iterations { get; }
    public Guid[] TeamIds { get; }
    public PlanningIntervalSprintMapping[] SprintMappings { get; }

    [JsonIgnore]
    public string AggregateType => "PlanningInterval";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
