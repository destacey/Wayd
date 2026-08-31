using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.ProductManagement.Application.DeploymentEnvironments.Dtos;

/// <summary>
/// A deployment target.
/// </summary>
public sealed record DeploymentEnvironmentDto
{
    public Guid Id { get; init; }
    public int Key { get; init; }
    public string Name { get; init; } = default!;

    /// <summary>
    /// What kind of target this is. Every delivery measure scoped to production counts on this rather
    /// than on the name, which is free text and endlessly varied.
    /// </summary>
    public EnvironmentCategory Category { get; init; }

    /// <summary>
    /// Position in a progressive rollout, lowest first. Environments sharing a ring are deployed to
    /// together; the value carries no meaning beyond ordering.
    /// </summary>
    public int RingOrder { get; init; }

    /// <summary>Whether this environment can still be deployed into.</summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// How many deployments have targeted it, so a caller can see what deactivating would affect.
    /// </summary>
    public int DeploymentCount { get; init; }
}
