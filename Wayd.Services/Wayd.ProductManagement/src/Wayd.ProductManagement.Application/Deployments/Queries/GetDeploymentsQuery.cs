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

        return await deployments
            .ProjectToType<DeploymentDto>(DeploymentDto.CreateTypeAdapterConfig())
            .OrderByDescending(d => d.StartedAt)
            .ToListAsync(cancellationToken);
    }
}
