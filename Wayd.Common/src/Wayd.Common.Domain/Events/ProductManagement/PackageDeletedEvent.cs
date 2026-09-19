using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A release package was deleted, with its manifest, its status history and every deployment of it.
/// </summary>
/// <remarks>
/// The deployments and the releases that listed it each record their own event, so this carries no
/// list of either.
/// </remarks>
public sealed record PackageDeletedEvent : DomainEvent<PackageDeletedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    public PackageDeletedEvent(Guid id, int key, string version, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Version = version;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Version { get; }

    [JsonIgnore]
    public string AggregateType => "ReleasePackage";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
