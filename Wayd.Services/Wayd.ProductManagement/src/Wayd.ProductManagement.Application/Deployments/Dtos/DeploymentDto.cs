using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.StatusWorkflows.Dtos;
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

    /// <summary>
    /// The release deployed, when this deployment carries a single release. Its <c>Name</c> is the
    /// release version, which is what a reader identifies it by.
    /// </summary>
    public NavigationDto? Release { get; init; }

    /// <summary>
    /// The package deployed, when several components shipped as one unit. Exactly one of this and
    /// <see cref="Release"/> is set.
    /// </summary>
    public NavigationDto? Package { get; init; }

    /// <summary>The environment reached.</summary>
    public NavigationDto Environment { get; init; } = default!;

    /// <summary>The environment's category as it stood when this deployment ran.</summary>
    public EnvironmentCategory EnvironmentCategory { get; init; }

    /// <summary>The build that actually shipped. Free text, never parsed.</summary>
    public string? ArtifactId { get; init; }

    public Instant StartedAt { get; init; }

    /// <summary>When it reached a terminal outcome, or null while still in flight.</summary>
    public Instant? CompletedAt { get; init; }

    /// <summary>Why it failed or was rolled back, where someone recorded a reason.</summary>
    public string? Reason { get; init; }

    /// <summary>The deployment's current status.</summary>
    public StatusNavigationDto Status { get; init; } = default!;

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
