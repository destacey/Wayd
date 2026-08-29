using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// One release or package reaching one environment, with a start, an end, an outcome, and its own
/// artifact identifier. The substrate every delivery metric is computed from.
/// </summary>
/// <remarks>
/// Exactly one of <see cref="ReleaseId"/> and <see cref="PackageId"/> is set. Where a package exists it
/// is the unit, because one pipeline run shipping fifteen services must count once, not fifteen times.
/// </remarks>
public sealed class Deployment : BaseAuditableEntity, IHasIdAndKey
{
    private Deployment() { }

    private Deployment(Guid? releaseId, Guid? packageId, Guid environmentId, EnvironmentCategory environmentCategory, string? artifactId, Instant startedAt, StatusRef status)
    {
        ReleaseId = releaseId;
        PackageId = packageId;
        EnvironmentId = environmentId;
        EnvironmentCategory = environmentCategory;
        ArtifactId = artifactId;
        StartedAt = startedAt;
        StatusId = status.StatusId;
        StatusCategory = status.Category;
        Outcome = (ProductStatusAlias)status.Alias;
    }

    /// <summary>
    /// The unique auto-generated key of the deployment. This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>The release deployed, when this deployment carries a single release.</summary>
    public Guid? ReleaseId { get; private init; }

    /// <summary>The package deployed, when several components shipped as one unit.</summary>
    public Guid? PackageId { get; private init; }

    /// <summary>The environment reached.</summary>
    public Guid EnvironmentId { get; private init; }

    /// <summary>
    /// The environment's category as it stood when this deployment ran.
    /// </summary>
    /// <remarks>
    /// Frozen rather than resolved through <see cref="EnvironmentId"/> on read, so reclassifying an
    /// environment cannot retroactively rewrite what past deployments counted as.
    /// </remarks>
    public EnvironmentCategory EnvironmentCategory { get; private init; }

    /// <summary>
    /// The build that actually shipped — <c>4.8.2.008</c> where the release version is <c>4.8.2</c>.
    /// </summary>
    /// <remarks>
    /// Separate from the release version: two builds of one release are two deployments. Free text,
    /// never parsed.
    /// </remarks>
    public string? ArtifactId
    {
        get;
        private set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>When the deployment began.</summary>
    public Instant StartedAt { get; private init; }

    /// <summary>
    /// When the deployment reached a terminal outcome, or <c>null</c> while it is still in flight.
    /// </summary>
    /// <remarks>
    /// Not a <see cref="FlexibleInstantRange"/>: its <c>EffectiveEnd</c> reads a null end as "runs
    /// forever", wrong for an in-flight deployment, and on a rollback this instant is the revert rather
    /// than the completion, which one range end cannot mean as well.
    /// </remarks>
    public Instant? CompletedAt { get; private set; }

    /// <summary>Why it failed or was rolled back, where someone recorded a reason.</summary>
    public string? Reason
    {
        get;
        private set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>The status this deployment currently holds, from its assigned workflow.</summary>
    public Guid StatusId { get; private set; }

    /// <summary>
    /// The status's category, denormalized so reads and invariants never need the workflow loaded.
    /// </summary>
    public StatusCategory StatusCategory { get; private set; }

    /// <summary>
    /// The well-known meaning of the current status, denormalized alongside it.
    /// </summary>
    /// <remarks>
    /// Metrics read the alias, not the status name, so a renamed outcome still counts. A deployment
    /// workflow cannot activate without supplying each required alias, so this is never silently absent.
    /// </remarks>
    public ProductStatusAlias Outcome { get; private set; }

    /// <summary>Whether this deployment has finished, however it finished.</summary>
    public bool IsComplete => CompletedAt is not null;

    /// <summary>
    /// Whether this deployment counts toward change failure rate.
    /// </summary>
    /// <remarks>
    /// The production check is part of the predicate, not the caller's job: a failure before production
    /// is a failure that was prevented, and counting it inverts the metric's meaning.
    /// </remarks>
    public bool IsChangeFailure =>
        EnvironmentCategory == EnvironmentCategory.Production
        && Outcome is ProductStatusAlias.Failed or ProductStatusAlias.RolledBack;

    /// <summary>
    /// Records that the deployment reached its environment.
    /// </summary>
    /// <param name="succeededStatus">
    /// The workflow status aliased <see cref="ProductStatusAlias.Succeeded"/>, resolved by the caller.
    /// </param>
    public Result Succeed(Instant completedAt, StatusRef succeededStatus, string environmentName, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(succeededStatus, nameof(succeededStatus));

        var guard = GuardCanComplete(completedAt);
        if (guard.IsFailure)
        {
            return guard;
        }

        Apply(completedAt, succeededStatus, reason: null);

        AddDomainEvent(new DeploymentSucceededEvent(
            Id, Key, ReleaseId, PackageId, EnvironmentId, environmentName, EnvironmentCategory,
            ArtifactId, completedAt, StatusId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Records that the deployment did not reach its environment.
    /// </summary>
    /// <param name="failedStatus">
    /// The workflow status aliased <see cref="ProductStatusAlias.Failed"/>, resolved by the caller.
    /// </param>
    public Result Fail(Instant completedAt, string? reason, StatusRef failedStatus, string environmentName, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(failedStatus, nameof(failedStatus));

        var guard = GuardCanComplete(completedAt);
        if (guard.IsFailure)
        {
            return guard;
        }

        Apply(completedAt, failedStatus, reason);

        AddDomainEvent(new DeploymentFailedEvent(
            Id, Key, ReleaseId, PackageId, EnvironmentId, environmentName, EnvironmentCategory,
            ArtifactId, Reason, completedAt, StatusId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Records that the deployment reached its environment and was then reverted.
    /// </summary>
    /// <param name="rolledBackStatus">
    /// The workflow status aliased <see cref="ProductStatusAlias.RolledBack"/>, resolved by the caller.
    /// </param>
    /// <remarks>
    /// Permitted only from a succeeded deployment. A failed or in-flight one never finished reaching its
    /// environment, so counting it as a rollback would inflate change failure rate.
    /// </remarks>
    public Result RollBack(Instant rolledBackAt, string? reason, StatusRef rolledBackStatus, string environmentName, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(rolledBackStatus, nameof(rolledBackStatus));

        if (Outcome == ProductStatusAlias.RolledBack)
        {
            return Result.Failure("This deployment has already been rolled back.");
        }

        if (Outcome == ProductStatusAlias.Failed)
        {
            return Result.Failure("A failed deployment never reached its environment and cannot be rolled back.");
        }

        if (!IsComplete)
        {
            return Result.Failure("A deployment that is still in progress cannot be rolled back. Record its outcome first.");
        }

        if (rolledBackAt < CompletedAt)
        {
            return Result.Failure("The rollback cannot be before the deployment completed.");
        }

        Apply(rolledBackAt, rolledBackStatus, reason);

        AddDomainEvent(new DeploymentRolledBackEvent(
            Id, Key, ReleaseId, PackageId, EnvironmentId, environmentName, EnvironmentCategory,
            ArtifactId, Reason, rolledBackAt, StatusId, actor, timestamp));

        return Result.Success();
    }

    private Result GuardCanComplete(Instant completedAt)
    {
        if (IsComplete)
        {
            return Result.Failure("This deployment has already completed.");
        }

        if (completedAt < StartedAt)
        {
            return Result.Failure("The completion cannot be before the deployment started.");
        }

        return Result.Success();
    }

    private void Apply(Instant completedAt, StatusRef status, string? reason)
    {
        CompletedAt = completedAt;
        StatusId = status.StatusId;
        StatusCategory = status.Category;
        Outcome = (ProductStatusAlias)status.Alias;
        Reason = reason;
    }

    /// <summary>
    /// Starts a deployment of a release or a package into an environment.
    /// </summary>
    /// <param name="environmentCategory">
    /// The environment's category, frozen onto the record. Supplied by the caller, which owns the
    /// environment lookup.
    /// </param>
    /// <param name="inProgressStatus">
    /// The workflow status the deployment begins in, resolved by the caller.
    /// </param>
    public static Result<Deployment> Create(
        Guid? releaseId,
        Guid? packageId,
        Guid environmentId,
        EnvironmentCategory environmentCategory,
        string? artifactId,
        Instant startedAt,
        StatusRef inProgressStatus,
        string environmentName,
        EventActor actor,
        Instant timestamp)
    {
        Guard.Against.Default(environmentId, nameof(environmentId));
        Guard.Against.Null(inProgressStatus, nameof(inProgressStatus));

        if (releaseId is null && packageId is null)
        {
            return Result.Failure<Deployment>("A deployment must be for either a release or a package.");
        }

        if (releaseId is not null && packageId is not null)
        {
            return Result.Failure<Deployment>(
                "A deployment is for either a release or a package, not both. Where a package exists it is the unit, so that one pipeline run counts once.");
        }

        var deployment = new Deployment(releaseId, packageId, environmentId, environmentCategory, artifactId, startedAt, inProgressStatus);

        // Deferred because Key is database-generated: an event raised here would carry Key 0.
        deployment.AddPostPersistenceAction(() => deployment.AddDomainEvent(new DeploymentStartedEvent(
            deployment.Id,
            deployment.Key,
            deployment.ReleaseId,
            deployment.PackageId,
            deployment.EnvironmentId,
            environmentName,
            deployment.ArtifactId,
            deployment.StartedAt,
            deployment.StatusId,
            actor,
            timestamp)));

        return Result.Success(deployment);
    }
}
