using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Dtos;

namespace Wayd.ProductManagement.Application.DeploymentEnvironments.Queries;

/// <summary>
/// What is running in each environment, in rollout order.
/// </summary>
/// <param name="IncludeInactive">
/// Retired environments are left out by default. They still hold the history of everything that ever
/// reached them, but nothing is running there.
/// </param>
public sealed record GetEnvironmentRolloutQuery(bool IncludeInactive = false)
    : IQuery<IReadOnlyCollection<EnvironmentRolloutDto>>;

/// <remarks>
/// Every set is reached through its DbSet and joined explicitly rather than through a navigation on
/// <c>Deployment</c>. Those navigations are populated by EF materialisation alone, so a query built on
/// them reads as null against any in-memory double and could only be exercised against a real database
/// — which this module has no suite for.
/// </remarks>
public sealed class GetEnvironmentRolloutQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetEnvironmentRolloutQuery, IReadOnlyCollection<EnvironmentRolloutDto>>
{
    private const int Succeeded = (int)ProductStatusAlias.Succeeded;
    private const int Failed = (int)ProductStatusAlias.Failed;
    private const int RolledBack = (int)ProductStatusAlias.RolledBack;

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    /// <summary>
    /// A candidate for one product's slot in one environment, before the two sources are reconciled.
    /// </summary>
    /// <remarks>
    /// A named type rather than a tuple because the projection is translated to SQL, and a
    /// <c>ValueTuple</c> is not something every provider can materialise into.
    /// </remarks>
    private sealed record Candidate(
        Guid EnvironmentId,
        Guid ProductId,
        Instant CompletedAt,
        int DeploymentKey,
        RolloutItemDto Item);

    public async Task<IReadOnlyCollection<EnvironmentRolloutDto>> Handle(
        GetEnvironmentRolloutQuery query, CancellationToken cancellationToken)
    {
        var environments = _productManagementDbContext.DeploymentEnvironments.AsQueryable();

        if (!query.IncludeInactive)
        {
            environments = environments.Where(e => e.IsActive);
        }

        var targets = await environments
            .OrderBy(e => e.RingOrder)
            .ThenBy(e => e.Name)
            .Select(e => new
            {
                e.Id,
                e.Key,
                e.Name,
                e.Category,
                e.RingOrder,
                e.IsActive,
            })
            .ToListAsync(cancellationToken);

        // Each source is reduced to one candidate per product in SQL, then the two are reconciled here.
        // The winner has to be chosen across both — a product can ship on its own one week and inside a
        // bundle the next — and one query spanning both would need a union whose halves join different
        // tables, which is not what LINQ's Concat translates to.
        var candidates = await RunningVersions().ToListAsync(cancellationToken);
        candidates.AddRange(await RunningPackagedComponents().ToListAsync(cancellationToken));

        var byEnvironment = candidates
            .GroupBy(candidate => candidate.EnvironmentId)
            .ToDictionary(
                environment => environment.Key,
                environment => environment
                    .GroupBy(candidate => candidate.ProductId)
                    .Select(product => product
                        .OrderByDescending(candidate => candidate.CompletedAt)
                        .ThenByDescending(candidate => candidate.DeploymentKey)
                        .First()
                        .Item)
                    .OrderBy(item => item.Product.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList());

        return targets
            .Select(e => new EnvironmentRolloutDto
            {
                Id = e.Id,
                Key = e.Key,
                Name = e.Name,
                Category = e.Category,
                RingOrder = e.RingOrder,
                IsActive = e.IsActive,
                Running = byEnvironment.TryGetValue(e.Id, out var items) ? items : [],
            })
            .ToList();
    }

    /// <summary>
    /// The latest version deployment of each product into each environment.
    /// </summary>
    /// <remarks>
    /// "Live" is the latest deployment whose outcome is still Succeeded. A rollback replaces its own
    /// deployment's status, so admitting only Succeeded also excludes the rolled-back one and leaves
    /// whatever preceded it as the answer — a failed attempt likewise leaves its predecessor in place.
    /// <para>
    /// Written as "nothing later beat it" rather than a grouped maximum so it translates on any
    /// provider. The tie-break on <c>Key</c> matters: two deployments completing in the same instant
    /// would otherwise both survive and the product would appear twice in one environment.
    /// </para>
    /// </remarks>
    private IQueryable<Candidate> RunningVersions() =>
        from deployment in _productManagementDbContext.Deployments
        join version in _productManagementDbContext.Versions
            on deployment.VersionId equals version.Id
        join product in _productManagementDbContext.Products
            on version.ProductId equals product.Id
        where deployment.StatusAliasValue == Succeeded
            && deployment.CompletedAt != null
            && !(from later in _productManagementDbContext.Deployments
                 join laterVersion in _productManagementDbContext.Versions
                     on later.VersionId equals laterVersion.Id
                 where later.EnvironmentId == deployment.EnvironmentId
                     && laterVersion.ProductId == version.ProductId
                     && later.StatusAliasValue == Succeeded
                     && later.CompletedAt != null
                     && (later.CompletedAt > deployment.CompletedAt
                         || (later.CompletedAt == deployment.CompletedAt && later.Key > deployment.Key))
                 select later).Any()
        select new Candidate(
            deployment.EnvironmentId,
            version.ProductId,
            deployment.CompletedAt!.Value,
            deployment.Key,
            new RolloutItemDto
            {
                DeploymentId = deployment.Id,
                DeploymentKey = deployment.Key,
                Product = NavigationDto.Create(product.Id, product.Key, product.Name),
                Version = NavigationDto.Create(version.Id, version.Key, version.Number),
                VersionLabel = version.Number,
                ArtifactId = deployment.ArtifactId,
                DeployedAt = deployment.CompletedAt!.Value,
                HasFailedAttemptSince =
                    _productManagementDbContext.Deployments.Any(attempt =>
                        attempt.EnvironmentId == deployment.EnvironmentId
                        && (attempt.StatusAliasValue == Failed || attempt.StatusAliasValue == RolledBack)
                        && attempt.CompletedAt > deployment.CompletedAt
                        && (_productManagementDbContext.Versions.Any(v =>
                                v.Id == attempt.VersionId && v.ProductId == version.ProductId)
                            || _productManagementDbContext.ReleasePackageComponents.Any(c =>
                                c.PackageId == attempt.PackageId && c.ProductId == version.ProductId))),
            });

    /// <summary>
    /// The latest packaged deployment of each component product into each environment.
    /// </summary>
    /// <remarks>
    /// A package deployment is expanded into its manifest so each component competes for its own
    /// product's slot. Superseding on the package id instead leaves <em>every bundle ever deployed</em>
    /// reported as running: successive bundles carry the same components, and no bundle supersedes a
    /// differently-numbered one.
    /// <para>
    /// Every manifest line counts, carried-forward as well as changed — a component that came along
    /// unchanged is still what is running.
    /// </para>
    /// </remarks>
    private IQueryable<Candidate> RunningPackagedComponents() =>
        from deployment in _productManagementDbContext.Deployments
        join package in _productManagementDbContext.ReleasePackages
            on deployment.PackageId equals package.Id
        join component in _productManagementDbContext.ReleasePackageComponents
            on package.Id equals component.PackageId
        join product in _productManagementDbContext.Products
            on component.ProductId equals product.Id
        where deployment.StatusAliasValue == Succeeded
            && deployment.CompletedAt != null
            && !(from later in _productManagementDbContext.Deployments
                 join laterComponent in _productManagementDbContext.ReleasePackageComponents
                     on later.PackageId equals laterComponent.PackageId
                 where later.EnvironmentId == deployment.EnvironmentId
                     && laterComponent.ProductId == component.ProductId
                     && later.StatusAliasValue == Succeeded
                     && later.CompletedAt != null
                     && (later.CompletedAt > deployment.CompletedAt
                         || (later.CompletedAt == deployment.CompletedAt && later.Key > deployment.Key))
                 select later).Any()
        select new Candidate(
            deployment.EnvironmentId,
            component.ProductId,
            deployment.CompletedAt!.Value,
            deployment.Key,
            new RolloutItemDto
            {
                DeploymentId = deployment.Id,
                DeploymentKey = deployment.Key,
                Product = NavigationDto.Create(product.Id, product.Key, product.Name),
                Version = component.VersionId == null
                    ? null
                    : NavigationDto.Create(component.VersionId.Value, 0, component.Version),
                VersionLabel = component.Version,
                Package = NavigationDto.Create(package.Id, package.Key, package.Version),
                ArtifactId = deployment.ArtifactId,
                DeployedAt = deployment.CompletedAt!.Value,
                HasFailedAttemptSince =
                    _productManagementDbContext.Deployments.Any(attempt =>
                        attempt.EnvironmentId == deployment.EnvironmentId
                        && (attempt.StatusAliasValue == Failed || attempt.StatusAliasValue == RolledBack)
                        && attempt.CompletedAt > deployment.CompletedAt
                        && (_productManagementDbContext.Versions.Any(v =>
                                v.Id == attempt.VersionId && v.ProductId == component.ProductId)
                            || _productManagementDbContext.ReleasePackageComponents.Any(c =>
                                c.PackageId == attempt.PackageId && c.ProductId == component.ProductId))),
            });
}
