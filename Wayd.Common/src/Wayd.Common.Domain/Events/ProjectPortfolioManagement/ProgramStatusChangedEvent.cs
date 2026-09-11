using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A program moved to a different status.
/// </summary>
/// <remarks>
/// One event covers activation, completion and cancellation rather than a type per verb, matching
/// <see cref="ProjectStatusChangedEvent"/> — see its remarks for why a fixed set of verbs is the wrong
/// shape for statuses that are on their way to being workflow-configurable.
/// <para>
/// Carries both ends, because a transition <em>is</em> the pair. Unlike a project, a program keeps no
/// status history, so this event is the only record a transition leaves: nothing existed before it to
/// share an id with, and nothing can be backfilled from.
/// </para>
/// </remarks>
public sealed record ProgramStatusChangedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProgramStatusChangedEvent(
        Guid id,
        int key,
        string fromStatus,
        LifecycleCategory fromCategory,
        string toStatus,
        LifecycleCategory toCategory,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        FromStatus = fromStatus;
        FromCategory = fromCategory;
        ToStatus = toStatus;
        ToCategory = toCategory;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    public string FromStatus { get; }
    public LifecycleCategory FromCategory { get; }
    public string ToStatus { get; }

    /// <summary>
    /// The lifecycle position the program moved into. This is what a consumer outside PPM branches on —
    /// never the status name, which a workflow administrator may rename.
    /// </summary>
    public LifecycleCategory ToCategory { get; }

    [JsonIgnore]
    public string AggregateType => "Program";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
