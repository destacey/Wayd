using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Models.Planning.Iterations;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Iterations;

/// <summary>
/// Tracking began for an iteration that existed before <see cref="IterationCreatedEventV2"/> was recorded. Carries
/// that event's payload, describing the iteration as it stood at <see cref="DomainEvent.Timestamp"/>.
/// </summary>
public sealed record IterationBaselinedEventV2 : BaselineEvent<IterationBaselinedEventV2, IterationCreatedEventV2>
{
    public IterationBaselinedEventV2(Guid id, int key, string name, IterationType type, IterationState state, IterationDateRange dateRange, Guid? teamId, Instant? recordCreatedOn, Guid? recordCreatedById, Instant timestamp)
        : base("Iteration", id, recordCreatedOn, recordCreatedById, timestamp, "2.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Type = type;
        State = state;
        DateRange = dateRange;
        TeamId = teamId;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public IterationType Type { get; }
    public IterationState State { get; }
    public IterationDateRange DateRange { get; }
    public Guid? TeamId { get; }
}
