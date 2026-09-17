namespace Wayd.Common.Domain.Activities;

/// <summary>
/// A record an <see cref="ActivityLogEntry"/> concerns besides the aggregate it was raised on.
/// </summary>
/// <remarks>
/// Its own type rather than the event's <c>AggregateReference</c>, so the stored row and the event contract can
/// change independently.
/// </remarks>
public sealed record ActivityLogRelatedAggregate(string AggregateType, Guid AggregateId);
