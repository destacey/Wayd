using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.Common.Domain.Events.ProductManagement;

/// <summary>
/// A deployment did not reach its environment.
/// </summary>
/// <remarks>
/// One of the two change-failure signals, and kept apart from <see cref="DeploymentRolledBackEvent"/>
/// because they describe genuinely different events: this one never reached users, a rollback did.
/// Collapsing them would push the interesting distinction into a field every consumer has to inspect.
/// <para>
/// Note for whoever computes change failure rate from this: a deployment that failed before reaching
/// production is a failure that was <em>prevented</em>. Counting it inflates the number while
/// describing the opposite of what happened, which is why the environment category travels with the
/// event.
/// </para>
/// </remarks>
public sealed record DeploymentFailedEvent : DomainEvent, IProductManagementEvent
{
    public DeploymentFailedEvent(
        Guid id,
        int key,
        Guid? versionId,
        Guid? packageId,
        Guid environmentId,
        string environmentName,
        EnvironmentCategory environmentCategory,
        string? artifactId,
        string? reason,
        Instant completedAt,
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
        EnvironmentCategory = environmentCategory;
        ArtifactId = artifactId;
        Reason = reason;
        CompletedAt = completedAt;
        StatusId = statusId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid? VersionId { get; }
    public Guid? PackageId { get; }
    public Guid EnvironmentId { get; }
    public string EnvironmentName { get; }
    public EnvironmentCategory EnvironmentCategory { get; }
    public string? ArtifactId { get; }
    public string? Reason { get; }
    public Instant CompletedAt { get; }
    public Guid StatusId { get; }
}
