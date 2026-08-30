using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.ProductManagement.Application.Deployments.Dtos;

/// <summary>
/// One release or package reaching one environment.
/// </summary>
/// <remarks>
/// <see cref="EnvironmentCategory"/> is frozen as it stood when the deployment ran, not resolved
/// through the environment on read: reclassifying an environment must not retroactively rewrite what
/// past deployments counted as.
/// </remarks>
public sealed record DeploymentDto
{
    public Guid Id { get; init; }
    public int Key { get; init; }

    /// <summary>The release deployed, when this deployment carries a single release.</summary>
    public Guid? ReleaseId { get; init; }
    public string? ReleaseVersion { get; init; }

    /// <summary>The package deployed, when several components shipped as one unit.</summary>
    public Guid? PackageId { get; init; }
    public string? PackageVersion { get; init; }

    public Guid EnvironmentId { get; init; }
    public string EnvironmentName { get; init; } = default!;

    /// <summary>The environment's category as it stood when this deployment ran.</summary>
    public EnvironmentCategory EnvironmentCategory { get; init; }

    /// <summary>The build that actually shipped. Free text, never parsed.</summary>
    public string? ArtifactId { get; init; }

    public Instant StartedAt { get; init; }

    /// <summary>When it reached a terminal outcome, or null while still in flight.</summary>
    public Instant? CompletedAt { get; init; }

    /// <summary>Why it failed or was rolled back, where someone recorded a reason.</summary>
    public string? Reason { get; init; }

    public Guid StatusId { get; init; }
    public string StatusName { get; init; } = default!;
    public StatusCategory StatusCategory { get; init; }

    /// <summary>
    /// The well-known meaning of the outcome. Metrics read this rather than the status name, so a
    /// renamed outcome still counts.
    /// </summary>
    public ProductStatusAlias Outcome { get; init; }

    /// <summary>Whether this deployment has finished, however it finished.</summary>
    public bool IsComplete { get; init; }

    /// <summary>
    /// Whether it counts toward change failure rate. A failure before production is a failure that was
    /// prevented, so only production counts.
    /// </summary>
    public bool IsChangeFailure { get; init; }
}
