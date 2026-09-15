using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.PlanningIntervalObjectives;

/// <summary>
/// An objective was created in a planning interval, for a team.
/// </summary>
/// <remarks>
/// An objective created by hand starts Not Started with no progress. An imported one arrives with its status,
/// progress and closed date already known, and records them as its creation.
/// </remarks>
public sealed record PlanningIntervalObjectiveCreatedEvent : DomainEvent<PlanningIntervalObjectiveCreatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    [JsonConstructor]
    public PlanningIntervalObjectiveCreatedEvent(
        Guid id,
        int key,
        Guid planningIntervalId,
        Guid teamId,
        string name,
        string? description,
        PlanningIntervalObjectiveType type,
        ObjectiveStatus status,
        double progress,
        bool isStretch,
        LocalDate? startDate,
        LocalDate? targetDate,
        Instant? closedDate,
        int? order,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        PlanningIntervalId = planningIntervalId;
        TeamId = teamId;
        Name = name;
        Description = description;
        Type = type;
        Status = status;
        Progress = progress;
        IsStretch = isStretch;
        StartDate = startDate;
        TargetDate = targetDate;
        ClosedDate = closedDate;
        Order = order;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid PlanningIntervalId { get; }
    public Guid TeamId { get; }
    public string Name { get; }
    public string? Description { get; }
    public PlanningIntervalObjectiveType Type { get; }
    public ObjectiveStatus Status { get; }
    public double Progress { get; }
    public bool IsStretch { get; }
    public LocalDate? StartDate { get; }
    public LocalDate? TargetDate { get; }
    public Instant? ClosedDate { get; }
    public int? Order { get; }

    [JsonIgnore]
    public string AggregateType => "PlanningIntervalObjective";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
