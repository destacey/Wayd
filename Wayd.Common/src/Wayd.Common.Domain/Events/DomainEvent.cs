using NodaTime;

namespace Wayd.Common.Domain.Events;

/// <summary>
/// Base for every domain event. Carries the <em>envelope</em> — the metadata a consumer needs in order to
/// act on an event, as opposed to the payload the event itself declares.
/// </summary>
/// <remarks>
/// <para>
/// The three envelope fields are assigned in three different places, by design:
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
/// <see cref="CorrelationId"/> is stamped by <c>BaseDbContext</c> where events are drained. It is the one
/// genuinely infrastructural field — the domain has no idea what request it is running inside.
/// <see cref="EventVersion"/> identifies the schema version of the event occurrence (e.g. "1.0").
/// </item>
/// </list>
/// </remarks>
public abstract record DomainEvent : IEvent
{
    /// <param name="actor">
    /// Who caused the event. Required — see the remarks on this type for why this is a constructor
    /// parameter rather than something stamped later.
    /// </param>
    /// <param name="eventVersion">
    /// The schema version of this domain event. Defaults to "1.0".
    /// </param>
    protected DomainEvent(EventActor actor, string eventVersion = "1.0")
    {
        ArgumentNullException.ThrowIfNull(actor);
        Actor = actor;
        EventVersion = string.IsNullOrWhiteSpace(eventVersion) ? "1.0" : eventVersion.Trim();
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
