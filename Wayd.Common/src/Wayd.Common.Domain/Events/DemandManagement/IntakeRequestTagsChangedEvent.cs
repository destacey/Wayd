using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// The tags an intake request is filed under changed.
/// </summary>
/// <remarks>
/// Carries the change and its result. <see cref="Added"/> and <see cref="Removed"/> are the fact: tagging along
/// an axis that takes one tag replaces the one it held, so one change can both add and remove.
/// <see cref="TagIds"/> is the full set afterwards, which a consumer keeping a copy applies whatever order the
/// deliveries arrive in. Summary tier, like the tags themselves.
/// </remarks>
public sealed record IntakeRequestTagsChangedEvent : DomainEvent<IntakeRequestTagsChangedEvent>, IDomainEventDescriptor, IIntakeRequestEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public static IntakeRequestReadTier ReadTier => IntakeRequestReadTier.Summary;

    [JsonConstructor]
    public IntakeRequestTagsChangedEvent(Guid id, int key, Guid[] added, Guid[] removed, Guid[] tagIds, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Added = [.. added];
        Removed = [.. removed];
        TagIds = [.. tagIds];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The tags this change applied.</summary>
    public Guid[] Added { get; }

    /// <summary>The tags this change took off, including any a single-value axis replaced.</summary>
    public Guid[] Removed { get; }

    /// <summary>Every tag the request carries after the change.</summary>
    public Guid[] TagIds { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequest";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
