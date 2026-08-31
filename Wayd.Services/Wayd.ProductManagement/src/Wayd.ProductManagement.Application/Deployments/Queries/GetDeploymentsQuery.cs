using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.Deployments.Dtos;

namespace Wayd.ProductManagement.Application.Deployments.Queries;

/// <summary>
/// Deployments, most recently started first.
/// </summary>
/// <param name="EnvironmentCategory">
/// Narrows to one kind of target. Filtering on the frozen category rather than joining the environment
/// is what keeps a reclassified environment from changing what past deployments count as.
/// </param>
public sealed record GetDeploymentsQuery(
    Guid? ReleaseId = null,
    Guid? PackageId = null,
    Guid? EnvironmentId = null,
    EnvironmentCategory? EnvironmentCategory = null,
    Instant? StartedOnOrAfter = null) : IQuery<IReadOnlyCollection<DeploymentDto>>;

public sealed class GetDeploymentsQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetDeploymentsQuery, IReadOnlyCollection<DeploymentDto>>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    public async Task<IReadOnlyCollection<DeploymentDto>> Handle(
        GetDeploymentsQuery query, CancellationToken cancellationToken)
    {
        var deployments = _productManagementDbContext.Deployments.AsNoTracking();

        if (query.ReleaseId is not null)
        {
            deployments = deployments.Where(d => d.ReleaseId == query.ReleaseId);
        }

        if (query.PackageId is not null)
        {
            deployments = deployments.Where(d => d.PackageId == query.PackageId);
        }

        if (query.EnvironmentId is not null)
        {
            deployments = deployments.Where(d => d.EnvironmentId == query.EnvironmentId);
        }

        if (query.EnvironmentCategory is not null)
        {
            deployments = deployments.Where(d => d.EnvironmentCategory == query.EnvironmentCategory);
        }

        if (query.StartedOnOrAfter is not null)
        {
            deployments = deployments.Where(d => d.StartedAt >= query.StartedOnOrAfter);
        }

        return await Project(deployments, _productManagementDbContext)
            .OrderByDescending(d => d.StartedAt)
            .ToListAsync(cancellationToken);
    }

    internal static IQueryable<DeploymentDto> Project(
        IQueryable<Domain.Models.Deployment> deployments, IProductManagementDbContext dbContext) =>
        deployments.Select(d => new DeploymentDto
        {
            Id = d.Id,
            Key = d.Key,
            Release = dbContext.Releases
                .Where(r => r.Id == d.ReleaseId)
                .Select(r => new NavigationDto { Id = r.Id, Key = r.Key, Name = r.Version })
                .FirstOrDefault(),
            Package = dbContext.ReleasePackages
                .Where(p => p.Id == d.PackageId)
                .Select(p => new NavigationDto { Id = p.Id, Key = p.Key, Name = p.Version })
                .FirstOrDefault(),
            Environment = dbContext.DeploymentEnvironments
                .Where(e => e.Id == d.EnvironmentId)
                .Select(e => new NavigationDto { Id = e.Id, Key = e.Key, Name = e.Name })
                .FirstOrDefault()!,
            EnvironmentCategory = d.EnvironmentCategory,
            ArtifactId = d.ArtifactId,
            StartedAt = d.StartedAt,
            CompletedAt = d.CompletedAt,
            Reason = d.Reason,
            Status = new StatusNavigationDto
            {
                Id = d.StatusId,
                Name = d.StatusName,
                Category = d.StatusCategory,
                Alias = d.StatusAliasValue,
            },
            Outcome = (ProductStatusAlias)d.StatusAliasValue,
            IsComplete = d.CompletedAt != null,
            IsChangeFailure =
                d.EnvironmentCategory == Common.Domain.Enums.ProductManagement.EnvironmentCategory.Production
                && (d.StatusAliasValue == (int)ProductStatusAlias.Failed
                    || d.StatusAliasValue == (int)ProductStatusAlias.RolledBack),
        });
}
