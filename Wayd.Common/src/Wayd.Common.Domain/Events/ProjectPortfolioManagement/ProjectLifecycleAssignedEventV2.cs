using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A lifecycle was assigned to a project that had none, creating its stages.
/// </summary>
/// <remarks>
/// Distinct from <see cref="ProjectLifecycleChangedEventV2"/> because the two are not the same fact: this
/// gives a project its stages for the first time and is a precondition of approval, while a replacement
/// discards existing stages and re-points the work sitting in them.
/// <para>
/// Supersedes <see cref="ProjectLifecycleAssignedEvent"/>, dropping its required <c>Name</c>, which described
/// the project rather than the change. A new type rather than a new version, because removing a required
/// member breaks every consumer written against the old shape.
/// </para>
/// </remarks>
public sealed record ProjectLifecycleAssignedEventV2 : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectLifecycleAssignedEventV2(
        Guid id,
        ProjectKey key,
        Guid lifecycleId,
        string lifecycleName,
        int stageCount,
        EventActor actor,
        Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
        LifecycleId = lifecycleId;
        LifecycleName = lifecycleName;
        StageCount = stageCount;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }
    public Guid LifecycleId { get; }
    public string LifecycleName { get; }

    /// <summary>How many stages the assignment created on the project.</summary>
    public int StageCount { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
