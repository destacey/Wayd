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
/// Carries both ends, because the move is the fact: a consumer watching for slippage needs where the dates
/// were as well as where they went, and cannot recover the earlier range from this entry alone.
/// </para>
/// </remarks>
public sealed record ProgramTimelineChangedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProgramTimelineChangedEvent(
        Guid id,
        int key,
        LocalDateRange? previousDateRange,
        LocalDateRange? dateRange,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        PreviousDateRange = previousDateRange;
        DateRange = dateRange;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The program's timeline before the change, or null when it had none.</summary>
    public LocalDateRange? PreviousDateRange { get; }

    /// <summary>
    /// The program's timeline after the change, or null when it was cleared.
    /// </summary>
    public LocalDateRange? DateRange { get; }

    [JsonIgnore]
    public string AggregateType => "Program";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
