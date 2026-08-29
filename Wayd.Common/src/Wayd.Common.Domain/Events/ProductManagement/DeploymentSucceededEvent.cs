using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A deployment reached its environment successfully.
/// </summary>
/// <remarks>
/// Carries the environment category so a consumer counting deployment frequency can filter to
/// production without loading the environment. Also the event time-to-restore measures <em>to</em>,
/// from a preceding failure in the same environment.
/// </remarks>
public sealed record DeploymentSucceededEvent : DomainEvent, IProductManagementEvent
{
    public DeploymentSucceededEvent(
        Guid id,
        int key,
        Guid? releaseId,
        Guid? packageId,
        Guid environmentId,
        string environmentName,
        EnvironmentCategory environmentCategory,
        string? artifactId,
        Instant completedAt,
        Guid statusId,
        EventActor actor,
        Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        ReleaseId = releaseId;
        PackageId = packageId;
        EnvironmentId = environmentId;
        EnvironmentName = environmentName;
        EnvironmentCategory = environmentCategory;
        ArtifactId = artifactId;
        CompletedAt = completedAt;
        StatusId = statusId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid? ReleaseId { get; }
    public Guid? PackageId { get; }
    public Guid EnvironmentId { get; }
    public string EnvironmentName { get; }

    /// <summary>
    /// The environment's category at the time of deployment. Captured on the event because
    /// reclassifying an environment later must not silently rewrite what this deployment counted as.
    /// </summary>
    public EnvironmentCategory EnvironmentCategory { get; }

    public string? ArtifactId { get; }
    public Instant CompletedAt { get; }
    public Guid StatusId { get; }
}
