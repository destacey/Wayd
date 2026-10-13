using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// The share of an employee's time given to teams changed: an allocation was added, ended, resized or moved
/// between teams.
/// </summary>
public sealed record EmployeeAllocationsChangedEvent : DomainEvent<EmployeeAllocationsChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public EmployeeAllocationsChangedEvent(Guid id, IReadOnlyCollection<EmployeeAllocationDetails> added, IReadOnlyCollection<EmployeeAllocationDetails> removed, IReadOnlyCollection<EmployeeAllocationDetails> allocations, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Added = added;
        Removed = removed;
        Allocations = allocations;

        Timestamp = timestamp;
    }

    /// <summary>The employee's id.</summary>
    public Guid Id { get; }

    /// <summary>The allocations in place after the change that were not in place before it.</summary>
    public IReadOnlyCollection<EmployeeAllocationDetails> Added { get; }

    /// <summary>The allocations in place before the change that are not in place after it.</summary>
    public IReadOnlyCollection<EmployeeAllocationDetails> Removed { get; }

    /// <summary>Every allocation the employee has after the change.</summary>
    public IReadOnlyCollection<EmployeeAllocationDetails> Allocations { get; }

    [JsonIgnore]
    public string AggregateType => "Employee";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
