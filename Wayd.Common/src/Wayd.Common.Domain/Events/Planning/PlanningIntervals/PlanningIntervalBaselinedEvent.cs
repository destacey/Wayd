using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.PlanningIntervals;

/// <summary>
/// Tracking began for a planning interval that existed before <see cref="PlanningIntervalCreatedEvent"/> was
/// recorded. Carries that event's payload, describing the interval as it stood at <see cref="DomainEvent.Timestamp"/>.
/// </summary>
public sealed record PlanningIntervalBaselinedEvent : BaselineEvent<PlanningIntervalBaselinedEvent, PlanningIntervalCreatedEvent>
{
    public PlanningIntervalBaselinedEvent(
        Guid id,
        int key,
        string name,
        string? description,
        LocalDateRange dateRange,
        bool objectivesLocked,
        PlanningIntervalIterationValues[] iterations,
        Guid[] teamIds,
        PlanningIntervalSprintMapping[] sprintMappings,
        Instant? recordCreatedOn,
        Guid? recordCreatedById,
        Instant timestamp)
        : base("PlanningInterval", id, recordCreatedOn, recordCreatedById, timestamp, "1.0")
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
}
