using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A lifecycle was assigned to a project that had none, creating its stages.
/// </summary>
/// <remarks>
/// Frozen at its published shape and never raised; <see cref="ProjectLifecycleAssignedEventV2"/> replaced it.
/// Kept so every payload written as this type still deserializes into it — its name and members are the
/// contract those payloads were written against, so neither may change.
/// </remarks>
[Obsolete("Superseded by ProjectLifecycleAssignedEventV2. Kept only to deserialize payloads already written as this type.")]
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
        : base(actor, "1.0")
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
