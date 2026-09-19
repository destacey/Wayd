using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A deployment was deleted, taking its status history with it.
/// </summary>
/// <remarks>
/// Carries what it shipped and where, so a consumer holding rollout or delivery measures can invalidate
/// the environment and the version or package it counted against without having kept a copy.
/// </remarks>
public sealed record DeploymentDeletedEvent : DomainEvent<DeploymentDeletedEvent>, IDomainEventDescriptor, IProductManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    public DeploymentDeletedEvent(
        Guid id,
        int key,
        Guid? versionId,
        Guid? packageId,
        Guid environmentId,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        VersionId = versionId;
        PackageId = packageId;
        EnvironmentId = environmentId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid? VersionId { get; }
    public Guid? PackageId { get; }
    public Guid EnvironmentId { get; }

    [JsonIgnore]
    public string AggregateType => "Deployment";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
