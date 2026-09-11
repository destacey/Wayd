using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// The strategic themes a project is tagged with changed.
/// </summary>
/// <remarks>
/// Carries both the change and its result, like the roles events. <see cref="Added"/> and
/// <see cref="Removed"/> are the fact, for a consumer that reacts to it. <see cref="StrategicThemes"/>
/// is the full set afterwards, matching <see cref="ProjectCreatedEvent.StrategicThemes"/>, for a consumer that
/// keeps a copy: applying the latest set is correct however deliveries were ordered or repeated, and
/// applying the deltas is not.
/// <para>
/// Supersedes <see cref="ProjectStrategicThemesChangedEvent"/>, dropping its required <c>Name</c>, which
/// described the project rather than the change. A new type rather than a new version, because removing a
/// required member breaks every consumer written against the old shape.
/// </para>
/// </remarks>
public sealed record ProjectStrategicThemesChangedEventV2 : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectStrategicThemesChangedEventV2(
        Guid id,
        ProjectKey key,
        Guid[] added,
        Guid[] removed,
        Guid[] strategicThemes,
        EventActor actor,
        Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
        Added = [.. added];
        Removed = [.. removed];
        StrategicThemes = [.. strategicThemes];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }

    /// <summary>The strategic theme ids this change tagged the project with.</summary>
    public Guid[] Added { get; }

    /// <summary>The strategic theme ids this change removed.</summary>
    public Guid[] Removed { get; }

    /// <summary>The strategic theme ids the project carries after the change.</summary>
    public Guid[] StrategicThemes { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
