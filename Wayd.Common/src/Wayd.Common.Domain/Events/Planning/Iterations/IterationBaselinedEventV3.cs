using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Models.Planning.Iterations;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Iterations;

/// <summary>
/// Tracking began for an iteration that existed before <see cref="IterationCreatedEventV3"/> was recorded. Carries
/// that event's payload, describing the iteration as it stood at <see cref="DomainEvent.Timestamp"/>.
/// </summary>
public sealed record IterationBaselinedEventV3 : BaselineEvent<IterationBaselinedEventV3, IterationCreatedEventV3>
{
    public IterationBaselinedEventV3(Guid id, int key, string name, IterationType type, IterationDateRange dateRange, Guid? teamId, Instant? recordCreatedOn, Guid? recordCreatedById, Instant timestamp)
        : base("Iteration", id, recordCreatedOn, recordCreatedById, timestamp, "3.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Type = type;
        DateRange = dateRange;
        TeamId = teamId;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public IterationType Type { get; }
    public IterationDateRange DateRange { get; }
    public Guid? TeamId { get; }
}
