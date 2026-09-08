using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A project's start and end dates were set, moved, or cleared.
/// </summary>
/// <remarks>
/// Raised separately from <see cref="ProjectDetailsUpdatedEvent"/> even though one command changes both:
/// the timeline is what lifecycle guards read, so moving it changes which transitions are legal, and a
/// consumer watching for slippage should not have to inspect a general update to find out.
/// <para>
/// Carries the new timeline only. Comparing an entry against the previous one of the same kind is how a
/// reader sees the movement, so repeating the old value in the payload would duplicate that.
/// </para>
/// </remarks>
public sealed record ProjectTimelineChangedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectTimelineChangedEvent(
        Guid id,
        ProjectKey key,
        string name,
        LocalDateRange? dateRange,
        EventActor actor,
        Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        Name = name;
        DateRange = dateRange;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }
    public string Name { get; }

    /// <summary>
    /// The project's timeline after the change, or null when it was cleared.
    /// </summary>
    public LocalDateRange? DateRange { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
