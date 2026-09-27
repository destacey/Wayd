using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A release package shipped.
/// </summary>
/// <remarks>
/// Supersedes <see cref="PackageReleasedEvent"/>, whose <c>ReleasedDate</c> held only a calendar date. This
/// records the moment instead, as <c>ReleasedAt</c>. A new type rather than a new version, because retyping
/// a member breaks every consumer written against the old shape.
/// </remarks>
public sealed record PackageReleasedEventV2 : DomainEvent<PackageReleasedEventV2>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StatusChanged;

    [JsonConstructor]
    public PackageReleasedEventV2(Guid id, int key, string version, Instant releasedAt, int componentCount, Guid statusId, EventActor actor, Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
        Version = version;
        ReleasedAt = releasedAt;
        ComponentCount = componentCount;
        StatusId = statusId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Version { get; }
    public Instant ReleasedAt { get; }
    public int ComponentCount { get; }
    public Guid StatusId { get; }

    [JsonIgnore]
    public string AggregateType => "ReleasePackage";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
