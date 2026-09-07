namespace Wayd.Common.Domain.Events;

/// <summary>
/// Implemented by domain events that belong to a specific aggregate root.
/// Enables child entities to declare their parent aggregate root, and aggregates to explicitly
/// provide their aggregate type, id, alternate key, and display name for activity logs and auditing.
/// </summary>
public interface IAggregateEvent
{
    /// <summary>
    /// The type name of the aggregate root (e.g. "ProjectPortfolio", "Team", "Project").
    /// </summary>
    string AggregateType { get; }

    /// <summary>
    /// The unique identifier of the aggregate root this event belongs to.
    /// </summary>
    Guid AggregateId { get; }
}
