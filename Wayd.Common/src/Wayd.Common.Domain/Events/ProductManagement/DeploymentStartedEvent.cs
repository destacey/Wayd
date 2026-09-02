using NodaTime;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A deployment of a version or package into an environment began.
/// </summary>
public sealed record DeploymentStartedEvent : DomainEvent, IProductManagementEvent
{
    public DeploymentStartedEvent(
        Guid id,
        int key,
        Guid? versionId,
        Guid? packageId,
        Guid environmentId,
        string environmentName,
        string? artifactId,
        Instant startedAt,
        Guid statusId,
        EventActor actor,
        Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        VersionId = versionId;
        PackageId = packageId;
        EnvironmentId = environmentId;
        EnvironmentName = environmentName;
        ArtifactId = artifactId;
        StartedAt = startedAt;
        StatusId = statusId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The version deployed, when this deployment carries a single version.</summary>
    public Guid? VersionId { get; }

    /// <summary>The package deployed, when several components shipped as one unit.</summary>
    public Guid? PackageId { get; }

    public Guid EnvironmentId { get; }
    public string EnvironmentName { get; }

    /// <summary>
    /// The build that actually shipped — <c>4.8.2.008</c> where the version is <c>4.8.2</c>. Kept apart
    /// from the version number so two builds of one version count as two deployments.
    /// </summary>
    public string? ArtifactId { get; }

    public Instant StartedAt { get; }
    public Guid StatusId { get; }
}
