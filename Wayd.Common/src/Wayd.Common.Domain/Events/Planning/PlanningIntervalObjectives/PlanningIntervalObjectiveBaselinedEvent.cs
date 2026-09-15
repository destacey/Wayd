using Wayd.Common.Domain.Enums.Planning;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.PlanningIntervalObjectives;

/// <summary>
/// Tracking began for a planning interval objective that existed before
/// <see cref="PlanningIntervalObjectiveCreatedEvent"/> was recorded. Carries that event's payload, describing the
/// objective as it stood at <see cref="DomainEvent.Timestamp"/>.
/// </summary>
public sealed record PlanningIntervalObjectiveBaselinedEvent : BaselineEvent<PlanningIntervalObjectiveBaselinedEvent, PlanningIntervalObjectiveCreatedEvent>
{
    public PlanningIntervalObjectiveBaselinedEvent(
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
        Instant? recordCreatedOn,
        Guid? recordCreatedById,
        Instant timestamp)
        : base("PlanningIntervalObjective", id, recordCreatedOn, recordCreatedById, timestamp, "1.0")
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
}
