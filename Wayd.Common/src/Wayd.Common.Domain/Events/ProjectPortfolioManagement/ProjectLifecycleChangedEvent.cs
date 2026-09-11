using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A project's lifecycle was swapped for a different one, replacing its stages and moving every task onto
/// the new set.
/// </summary>
/// <remarks>
/// Frozen at its published shape and never raised; <see cref="ProjectLifecycleChangedEventV2"/> replaced it.
/// Kept so every payload written as this type still deserializes into it — its name and members are the
/// contract those payloads were written against, so neither may change.
/// </remarks>
[Obsolete("Superseded by ProjectLifecycleChangedEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record ProjectLifecycleChangedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectLifecycleChangedEvent(
        Guid id,
        ProjectKey key,
        string name,
        Guid lifecycleId,
        string lifecycleName,
        int stageCount,
        int remappedTaskCount,
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
        RemappedTaskCount = remappedTaskCount;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }
    public string Name { get; }

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
