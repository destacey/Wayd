using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.ProductManagement.Application.DeploymentEnvironments.Dtos;

/// <summary>
/// What one environment is running right now, derived from its deployments rather than stored.
/// </summary>
public sealed record EnvironmentRolloutDto
{
    public Guid Id { get; init; }
    public int Key { get; init; }
    public string Name { get; init; } = default!;

    /// <summary>What kind of target this is.</summary>
    public EnvironmentCategory Category { get; init; }

    /// <summary>Position in a progressive rollout, lowest first.</summary>
    public int RingOrder { get; init; }

    /// <summary>Whether the environment can still be deployed into.</summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Everything currently live here, one entry per product. Empty where nothing has ever succeeded
    /// into it, which is a complete answer rather than missing data.
    /// </summary>
    public IReadOnlyCollection<RolloutItemDto> Running { get; init; } = [];
}

/// <summary>
/// One product currently live in an environment, and the deployment that put it there.
/// </summary>
/// <remarks>
/// <strong>Always keyed on the product</strong>, never on how it arrived. A package is a way a version
/// reaches an environment, not a thing that runs alongside the products it contains — so a package
/// deployment is expanded into its manifest and each component competes for its own product's slot.
/// Keying on the package instead would leave every bundle ever deployed listed as running, because two
/// successive bundles carry the same components and neither supersedes the other by id.
/// <para>
/// The entry is the latest deployment that <em>succeeded and was not rolled back</em>, not the latest
/// deployment. A failed attempt leaves its predecessor running, and a rollback takes its own deployment
/// out of the running while leaving the one before it in — so "what is here" and "what happened last"
/// are different questions with different answers.
/// </para>
/// </remarks>
public sealed record RolloutItemDto
{
    /// <summary>The deployment this was read from, so a caller can open the record behind the answer.</summary>
    public Guid DeploymentId { get; init; }
    public int DeploymentKey { get; init; }

    /// <summary>The product running.</summary>
    public NavigationDto Product { get; init; } = default!;

    /// <summary>
    /// The version record, where one is recorded in Wayd.
    /// </summary>
    /// <remarks>
    /// Null for a component a package carried that was never cut here — the manifest keeps its version
    /// as text alone, which is the whole reason <see cref="VersionLabel"/> exists separately.
    /// </remarks>
    public NavigationDto? Version { get; init; }

    /// <summary>
    /// The version as it should be read, whether or not a version record backs it. Free text.
    /// </summary>
    public string VersionLabel { get; init; } = default!;

    /// <summary>
    /// The package that carried it, where it arrived inside one rather than on its own.
    /// </summary>
    public NavigationDto? Package { get; init; }

    /// <summary>The build that actually shipped, where one was recorded. Free text, never parsed.</summary>
    public string? ArtifactId { get; init; }

    /// <summary>When it reached the environment.</summary>
    public Instant DeployedAt { get; init; }

    /// <summary>
    /// Whether a later deployment touching this product failed or was rolled back here.
    /// </summary>
    /// <remarks>
    /// The running version is still the honest answer to "what is here", but on its own it hides that
    /// someone has since tried to move past it and could not. A reader needs both.
    /// </remarks>
    public bool HasFailedAttemptSince { get; init; }
}
