using Wayd.ProductManagement.Application.Deployments.Dtos;

namespace Wayd.ProductManagement.Application.Deployments.Queries;

/// <summary>
/// A single deployment by id, or <c>null</c> when it does not exist.
/// </summary>
public sealed record GetDeploymentQuery(Guid Id) : IQuery<DeploymentDto?>;

public sealed class GetDeploymentQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetDeploymentQuery, DeploymentDto?>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    public async Task<DeploymentDto?> Handle(GetDeploymentQuery query, CancellationToken cancellationToken)
    {
        var deployments = _productManagementDbContext.Deployments
            .AsNoTracking()
            .Where(d => d.Id == query.Id);

        return await GetDeploymentsQueryHandler
            .Project(deployments, _productManagementDbContext)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
