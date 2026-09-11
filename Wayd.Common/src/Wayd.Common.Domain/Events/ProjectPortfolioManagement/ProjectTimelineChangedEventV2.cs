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
/// <para>
/// Supersedes <see cref="ProjectTimelineChangedEvent"/>, dropping its required <c>Name</c>, which described
/// the project rather than the change. A new type rather than a new version, because removing a required
/// member breaks every consumer written against the old shape.
/// </para>
/// </remarks>
public sealed record ProjectTimelineChangedEventV2 : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectTimelineChangedEventV2(
        Guid id,
        ProjectKey key,
        LocalDateRange? dateRange,
        EventActor actor,
        Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
        DateRange = dateRange;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }

    /// <summary>
    /// The project's timeline after the change, or null when it was cleared.
    /// </summary>
    public LocalDateRange? DateRange { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
