using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events;

/// <summary>
/// Base for an aggregate's baseline event: the record as it stood when tracking began, for a record that
/// existed before its <typeparamref name="TCreated"/> event did.
/// </summary>
/// <typeparam name="TSelf">The concrete baseline type.</typeparam>
/// <typeparam name="TCreated">
/// The aggregate's creation event. The baseline carries exactly its payload and version, so anything folding a
/// history starts the same way from either; <c>BaselineEventConventionTests</c> holds them together.
/// </typeparam>
/// <remarks>
/// <para>
/// Attributed to <see cref="EventActor.System"/>: the platform started tracking, nobody changed the record.
/// </para>
/// <para>
/// Its <see cref="DomainEvent.EventId"/> is derived from the aggregate rather than generated, so a record has
/// exactly one baseline id however many times one is written — a second write collides with the first row in
/// <c>ActivityLogs</c> instead of adding a duplicate. The id does not depend on the type's generation, so a
/// baseline written as a later generation is still the same baseline.
/// </para>
/// </remarks>
public abstract record BaselineEvent<TSelf, TCreated> : DomainEvent<TSelf>, IDomainEventDescriptor, IBaselineEvent
    where TSelf : BaselineEvent<TSelf, TCreated>
    where TCreated : DomainEvent, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Baseline;

    /// <param name="aggregateType">The aggregate type <typeparamref name="TCreated"/> declares.</param>
    /// <param name="aggregateId">The record the baseline describes.</param>
    /// <param name="recordCreatedOn">See <see cref="RecordCreatedOn"/>.</param>
    /// <param name="recordCreatedById">See <see cref="RecordCreatedById"/>.</param>
    /// <param name="timestamp">When the baseline was written, which is the moment it describes.</param>
    /// <param name="eventVersion">The version of <typeparamref name="TCreated"/> whose shape this carries.</param>
    protected BaselineEvent(string aggregateType, Guid aggregateId, Instant? recordCreatedOn, Guid? recordCreatedById, Instant timestamp, string eventVersion)
        : base(EventActor.System, eventVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

        AggregateType = aggregateType;
        AggregateId = aggregateId;
        RecordCreatedOn = recordCreatedOn;
        RecordCreatedById = recordCreatedById;
        EventId = BaselineEventId.For(aggregateType, aggregateId);
        Timestamp = timestamp;
    }

    /// <summary>
    /// When the record was created, which a <typeparamref name="TCreated"/> event records as its own
    /// <see cref="DomainEvent.Timestamp"/>. <c>null</c> when it was not recorded.
    /// </summary>
    /// <remarks>
    /// Only the creation date: the rest of the payload describes the record at <see cref="DomainEvent.Timestamp"/>,
    /// not as it was created.
    /// </remarks>
    public Instant? RecordCreatedOn { get; }

    /// <summary>
    /// The employee who created the record, where the creating account was linked to one. <c>null</c> when it
    /// was not recorded, or was created by the system, an import or an unlinked account.
    /// </summary>
    public Guid? RecordCreatedById { get; }

    [JsonIgnore]
    public string AggregateType { get; }

    [JsonIgnore]
    public Guid AggregateId { get; }
}
