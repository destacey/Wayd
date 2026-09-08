using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A lifecycle was assigned to a project that had none, creating its stages.
/// </summary>
/// <remarks>
/// Distinct from <see cref="ProjectLifecycleChangedEvent"/> because the two are not the same fact: this
/// gives a project its stages for the first time and is a precondition of approval, while a replacement
/// discards existing stages and re-points the work sitting in them.
/// </remarks>
public sealed record ProjectLifecycleAssignedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectLifecycleAssignedEvent(
        Guid id,
        ProjectKey key,
        string name,
        Guid lifecycleId,
        string lifecycleName,
        int stageCount,
        EventActor actor,
        Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        Name = name;
        LifecycleId = lifecycleId;
        LifecycleName = lifecycleName;
        StageCount = stageCount;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }
    public string Name { get; }
    public Guid LifecycleId { get; }
    public string LifecycleName { get; }

    /// <summary>How many stages the assignment created on the project.</summary>
    public int StageCount { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
