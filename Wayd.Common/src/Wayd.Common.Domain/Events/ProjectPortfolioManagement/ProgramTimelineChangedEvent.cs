using System.Text.Json.Serialization;
using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A program's start and end dates were set, moved, or cleared.
/// </summary>
/// <remarks>
/// Raised separately from <see cref="ProgramDetailsUpdatedEvent"/> even though one command changes both:
/// the timeline is what the lifecycle guards read, so moving it changes which transitions are legal.
/// <para>
/// Carries the new timeline only. Comparing an entry against the previous one of the same kind is how a
/// reader sees the movement.
/// </para>
/// </remarks>
public sealed record ProgramTimelineChangedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProgramTimelineChangedEvent(
        Guid id,
        int key,
        LocalDateRange? dateRange,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        DateRange = dateRange;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>
    /// The program's timeline after the change, or null when it was cleared.
    /// </summary>
    public LocalDateRange? DateRange { get; }

    [JsonIgnore]
    public string AggregateType => "Program";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
