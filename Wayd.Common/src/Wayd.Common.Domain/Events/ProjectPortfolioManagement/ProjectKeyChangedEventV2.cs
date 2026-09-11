using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A project's key changed, cascading to the key of every task under it.
/// </summary>
/// <remarks>
/// Split out of <see cref="ProjectDetailsUpdatedEvent"/> because the key is an external identifier rather
/// than a detail: imports, links and agent tools address a project by it, so a consumer needs to see this
/// as a change of identity rather than as one of several fields that might have moved.
/// <para>
/// Delivered durably alongside the other <c>Project*</c> events, because the Work module's projection
/// stores the key and would go stale the moment this stopped riding on the details event.
/// </para>
/// <para>
/// Supersedes <see cref="ProjectKeyChangedEvent"/>, dropping its required <c>Name</c>, which described the
/// project rather than the change. A new type rather than a new version, because removing a required member
/// breaks every consumer written against the old shape.
/// </para>
/// </remarks>
public sealed record ProjectKeyChangedEventV2 : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectKeyChangedEventV2(
        Guid id,
        ProjectKey previousKey,
        ProjectKey key,
        EventActor actor,
        Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        PreviousKey = previousKey;
        Key = key;

        Timestamp = timestamp;
    }

    public Guid Id { get; }

    /// <summary>
    /// The key the project was addressed by before the change. Links and imports written against it stop
    /// resolving, so a consumer holding the old key needs it to know what to rewrite.
    /// </summary>
    public ProjectKey PreviousKey { get; }

    /// <summary>The project's key after the change.</summary>
    public ProjectKey Key { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
