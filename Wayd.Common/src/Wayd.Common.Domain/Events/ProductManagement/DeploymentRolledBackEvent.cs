using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A deployment reached its environment and was then reverted.
/// </summary>
/// <remarks>
/// The strongest change-failure signal available without an incident feed: unlike a pipeline failure,
/// a rollback means something actually reached users and then had to be undone. Time to restore
/// measures from here to the next success in the same environment.
/// </remarks>
public sealed record DeploymentRolledBackEvent : DomainEvent, IProductManagementEvent
{
    public DeploymentRolledBackEvent(
        Guid id,
        int key,
        Guid? releaseId,
        Guid? packageId,
        Guid environmentId,
        string environmentName,
        EnvironmentCategory environmentCategory,
        string? artifactId,
        string? reason,
        Instant rolledBackAt,
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
        Reason = reason;
        RolledBackAt = rolledBackAt;
        StatusId = statusId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid? ReleaseId { get; }
    public Guid? PackageId { get; }
    public Guid EnvironmentId { get; }
    public string EnvironmentName { get; }
    public EnvironmentCategory EnvironmentCategory { get; }
    public string? ArtifactId { get; }
    public string? Reason { get; }
    public Instant RolledBackAt { get; }
    public Guid StatusId { get; }
}
