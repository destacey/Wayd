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
/// </remarks>
public sealed record ProjectKeyChangedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectKeyChangedEvent(
        Guid id,
        ProjectKey key,
        string name,
        EventActor actor,
        Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        Name = name;

        Timestamp = timestamp;
    }

    public Guid Id { get; }

    /// <summary>The project's key after the change.</summary>
    public ProjectKey Key { get; }

    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
