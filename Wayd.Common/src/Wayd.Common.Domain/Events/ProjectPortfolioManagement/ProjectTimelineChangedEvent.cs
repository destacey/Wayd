using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A project's start and end dates were set, moved, or cleared.
/// </summary>
/// <remarks>
/// Frozen at its published shape and never raised; <see cref="ProjectTimelineChangedEventV2"/> replaced it.
/// Kept so every payload written as this type still deserializes into it — its name and members are the
/// contract those payloads were written against, so neither may change.
/// </remarks>
[Obsolete("Superseded by ProjectTimelineChangedEventV2. Kept only to deserialize payloads already written as this type.")]
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
        : base(actor, "1.0")
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
