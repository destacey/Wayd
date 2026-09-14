namespace Wayd.Common.Domain.Events;

/// <summary>
/// An event recording that tracking began for a record that already existed. Implemented only through
/// <see cref="BaselineEvent{TSelf, TCreated}"/>.
/// </summary>
/// <remarks>
/// A baseline is written to the activity log and never published: <c>BaseDbContext</c> records it and
/// dispatches it nowhere. It carries the whole record, which no routed event may, and it is not an
/// occurrence any consumer can act on — nothing happened to the record, the log simply started watching.
/// </remarks>
public interface IBaselineEvent : IAggregateEvent
{
}
