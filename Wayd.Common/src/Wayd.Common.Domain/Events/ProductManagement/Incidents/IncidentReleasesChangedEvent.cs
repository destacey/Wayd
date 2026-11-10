using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement.Incidents;

/// <summary>The releases an incident points at were replaced.</summary>
public sealed record IncidentReleasesChangedEvent : DomainEvent<IncidentReleasesChangedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public IncidentReleasesChangedEvent(
        Guid id,
        int key,
        IReadOnlyCollection<Guid> addedReleaseIds,
        IReadOnlyCollection<Guid> removedReleaseIds,
        IReadOnlyCollection<Guid> releaseIds,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        AddedReleaseIds = addedReleaseIds;
        RemovedReleaseIds = removedReleaseIds;
        ReleaseIds = releaseIds;

        Timestamp = timestamp;
    }

    /// <summary>The incident.</summary>
    public Guid Id { get; }

    /// <summary>The incident's key.</summary>
    public int Key { get; }

    /// <summary>Releases added by this change.</summary>
    public IReadOnlyCollection<Guid> AddedReleaseIds { get; }

    /// <summary>Releases removed by this change.</summary>
    public IReadOnlyCollection<Guid> RemovedReleaseIds { get; }

    /// <summary>The releases the incident points at after the change.</summary>
    public IReadOnlyCollection<Guid> ReleaseIds { get; }

    [JsonIgnore]
    public string AggregateType => "Incident";

    [JsonIgnore]
    public Guid AggregateId => Id;
}
