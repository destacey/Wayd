using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Domain.Models;

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
    /// The version deployed, when this deployment carries a single version. Its <c>Name</c> is the
    /// version number, which is what a reader identifies it by.
    /// </summary>
    public NavigationDto? Version { get; init; }

    /// <summary>
    /// The package deployed, when several components shipped as one unit. Exactly one of this and
    /// <see cref="Version"/> is set.
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

    /// <summary>
    /// Maps the deployment, for <c>ProjectToType</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="Outcome"/>, <see cref="IsComplete"/> and <see cref="IsChangeFailure"/> are same-named
    /// domain members that EF is told to ignore, so convention would bind them to expressions no
    /// provider can translate. Each is recomputed from the columns that do exist, and the
    /// change-failure predicate must keep agreeing with the domain's.
    /// <para>
    /// A release and a package are identified by their version rather than their optional name. The
    /// environment is not, so it maps by convention.
    /// </para>
    /// </remarks>
    public static TypeAdapterConfig CreateTypeAdapterConfig()
    {
        var config = new TypeAdapterConfig();

        config.NewConfig<Deployment, DeploymentDto>()
            .Map(dto => dto.Version, d => d.Version != null
                ? NavigationDto.Create(d.Version.Id, d.Version.Key, d.Version.Number)
                : null)
            .Map(dto => dto.Package, d => d.Package != null
                ? NavigationDto.Create(d.Package.Id, d.Package.Key, d.Package.Version)
                : null)
            .Map(dto => dto.Status, d => new StatusNavigationDto
            {
                Id = d.StatusId,
                Name = d.StatusName,
                Category = d.StatusCategory,
                Alias = d.StatusAliasValue,
            })
            .Map(dto => dto.Outcome, d => (ProductStatusAlias)d.StatusAliasValue)
            .Map(dto => dto.IsComplete, d => d.CompletedAt != null)
            .Map(dto => dto.IsChangeFailure, d =>
                d.EnvironmentCategory == EnvironmentCategory.Production
                && (d.StatusAliasValue == (int)ProductStatusAlias.Failed
                    || d.StatusAliasValue == (int)ProductStatusAlias.RolledBack));

        return config;
    }
}
