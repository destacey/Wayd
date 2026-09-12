using NodaTime;

namespace Wayd.Common.Domain.Events;

/// <summary>
/// Base for every domain event. Carries the <em>envelope</em> — the metadata a consumer needs in order to
/// act on an event, as opposed to the payload the event itself declares.
/// </summary>
/// <remarks>
/// <para>
/// The envelope fields are assigned in different places, by design:
/// </para>
/// <list type="bullet">
/// <item>
/// <see cref="EventId"/> is assigned here, at construction. Identity is not a decision any caller should
/// make — the only correct value is a fresh one — so requiring it at the call site would add a parameter
/// nobody can choose well and could get wrong by passing a duplicate.
/// </item>
/// <item>
/// <see cref="Actor"/> is a <strong>required constructor parameter</strong>, so an event cannot be
/// constructed without saying who caused it and a missing actor is a compile error rather than a null
/// discovered later in a notification. The domain requires it but never resolves it: Domain has zero
/// dependencies and must not learn about <c>ICurrentUser</c> or ambient state. The application layer
/// decides, which is what lets an import attribute its events to the import rather than to whoever
/// happened to start it.
/// </item>
/// <item>
/// <see cref="EventVersion"/> is a <strong>required constructor parameter</strong> naming the schema version
/// of this event's published shape (e.g. "1.0"), for the reason given on that parameter.
/// </item>
/// </list>
/// <para>
/// The correlation id that ties an event back to the request that caused it is deliberately <em>not</em> here.
/// The domain has no idea what request it is running inside, so it is stamped where events are drained, onto
/// the <c>ActivityLogEntry</c> row and onto the outbox envelope's <c>DeliveryOptions</c>.
/// </para>
/// <para>
/// <strong>The envelope carries no ordering, and must not grow one.</strong> The events of one command land
/// microseconds apart at best, and share a <see cref="Timestamp"/> exactly wherever the caller reads the clock
/// once for a whole batch, so the timestamp does not order them. Two things stop a sequence
/// number from being the answer. Durable delivery is at-least-once through the outbox with no guarantee of
/// order between messages, so a field saying where an event sits would advertise a promise the transport does
/// not keep and invite a consumer to depend on it — which is why payloads carry the set after a change rather
/// than a delta, so that applying them in any order and more than once is still correct. And the only place
/// that knows the true order events were raised in is <c>AddDomainEvent</c>, where numbering across aggregates
/// would need an ambient counter, in a layer that has no dependencies and resolves nothing for itself.
/// </para>
/// <para>
/// Order is a property of <em>recording</em> a fact, not of the fact, so it lives on the ledger: the
/// <c>ActivityLogs</c> row carries an <c>Ordinal</c> counted as the events are drained, and reads sort by
/// timestamp then by it. Within one aggregate that order is faithful — events are drained from an ordered list
/// in the order they were raised — and every activity view reads a single aggregate.
/// </para>
/// </remarks>
public abstract record DomainEvent : IEvent
{
    /// <param name="actor">
    /// Who caused the event. Required — see the remarks on this type for why this is a constructor
    /// parameter rather than something stamped later.
    /// </param>
    /// <param name="eventVersion">
    /// The schema version of this event's published shape, e.g. "1.0". Required rather than defaulted: a
    /// default hides the one number a contract change has to bump deliberately.
    /// </param>
    protected DomainEvent(EventActor actor, string eventVersion)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventVersion);
        Actor = actor;
        EventVersion = eventVersion.Trim();
    }

    /// <summary>
    /// When the event occurred, as supplied by the aggregate that raised it.
    /// </summary>
    public Instant Timestamp { get; protected set; }

    /// <summary>
    /// Stable identity for this event occurrence.
    /// </summary>
    /// <remarks>
    /// Durable delivery is at-least-once by design (see <c>DurableEventRoutes</c>), so a handler may see the
    /// same event more than once. This is the value to deduplicate on: it is assigned once at construction
    /// and travels with the event through the outbox, so every redelivery of one occurrence carries the same
    /// id.
    /// </remarks>
    public Guid EventId { get; init; } = Guid.CreateVersion7();

    /// <summary>
    /// Who caused the event — the mechanism, and the person behind it where there is one.
    /// </summary>
    public EventActor Actor { get; init; }

    /// <summary>
    /// The schema version of this domain event (e.g. "1.0").
    /// </summary>
    public virtual string EventVersion { get; init; } = "1.0";
}
