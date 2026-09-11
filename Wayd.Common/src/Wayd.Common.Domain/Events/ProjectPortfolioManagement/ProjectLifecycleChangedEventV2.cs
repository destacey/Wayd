using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A project's lifecycle was swapped for a different one, replacing its stages and moving every task onto
/// the new set.
/// </summary>
/// <remarks>
/// The most destructive operation on the aggregate: the old stages are discarded outright and each task is
/// re-pointed, with tasks whose stage was not mapped falling to the first stage of the new lifecycle.
/// Recording how many tasks moved is what makes a surprising result afterwards traceable.
/// <para>
/// Supersedes <see cref="ProjectLifecycleChangedEvent"/>, dropping its required <c>Name</c>, which described
/// the project rather than the change. A new type rather than a new version, because removing a required
/// member breaks every consumer written against the old shape.
/// </para>
/// </remarks>
public sealed record ProjectLifecycleChangedEventV2 : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectLifecycleChangedEventV2(
        Guid id,
        ProjectKey key,
        Guid lifecycleId,
        string lifecycleName,
        int stageCount,
        int remappedTaskCount,
        EventActor actor,
        Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
        LifecycleId = lifecycleId;
        LifecycleName = lifecycleName;
        StageCount = stageCount;
        RemappedTaskCount = remappedTaskCount;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }

    /// <summary>The lifecycle the project now follows.</summary>
    public Guid LifecycleId { get; }

    public string LifecycleName { get; }

    /// <summary>How many stages the new lifecycle put on the project.</summary>
    public int StageCount { get; }

    /// <summary>How many tasks were moved onto a stage of the new lifecycle.</summary>
    public int RemappedTaskCount { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
