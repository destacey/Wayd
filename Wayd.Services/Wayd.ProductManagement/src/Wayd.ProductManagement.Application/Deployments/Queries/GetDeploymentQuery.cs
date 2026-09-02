using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.ProductManagement.Application.Deployments.Dtos;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Deployments.Queries;

/// <summary>
/// A single deployment by id or key, or <c>null</c> when it does not exist.
/// </summary>
/// <remarks>
/// Accepts either so a URL can carry the short integer key a reader can recognise, rather than a
/// GUID, matching how the other modules address a record.
/// </remarks>
public sealed record GetDeploymentQuery : IQuery<DeploymentDto?>
{
    public GetDeploymentQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Deployment>();
    }

    public Expression<Func<Deployment, bool>> IdOrKeyFilter { get; }
}

public sealed class GetDeploymentQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetDeploymentQuery, DeploymentDto?>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    public async Task<DeploymentDto?> Handle(GetDeploymentQuery query, CancellationToken cancellationToken)
    {
        return await _productManagementDbContext.Deployments
            .Where(query.IdOrKeyFilter)
            .ProjectToType<DeploymentDto>(DeploymentDto.CreateTypeAdapterConfig())
            .FirstOrDefaultAsync(cancellationToken);
    }
}
