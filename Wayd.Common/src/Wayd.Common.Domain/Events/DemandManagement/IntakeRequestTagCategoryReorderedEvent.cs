using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake request tag category moved to another position in the list.
/// </summary>
/// <remarks>
/// Raised by each category whose position changed, so reordering the list records one entry per category moved.
/// </remarks>
public sealed record IntakeRequestTagCategoryReorderedEvent : DomainEvent<IntakeRequestTagCategoryReorderedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public IntakeRequestTagCategoryReorderedEvent(Guid id, int fromOrder, int toOrder, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        FromOrder = fromOrder;
        ToOrder = toOrder;

        Timestamp = timestamp;
    }

    public Guid Id { get; }

    /// <summary>The category's position before the move, lowest first.</summary>
    public int FromOrder { get; }

    /// <summary>The category's position after the move, lowest first.</summary>
    public int ToOrder { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequestTagCategory";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
