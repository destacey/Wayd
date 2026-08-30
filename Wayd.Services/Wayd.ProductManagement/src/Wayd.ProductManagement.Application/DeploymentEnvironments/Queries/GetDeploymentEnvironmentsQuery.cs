using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Dtos;

namespace Wayd.ProductManagement.Application.DeploymentEnvironments.Queries;

/// <summary>
/// The deployment targets, in rollout order.
/// </summary>
public sealed record GetDeploymentEnvironmentsQuery(bool? IsActive = null, EnvironmentCategory? Category = null)
    : IQuery<IReadOnlyCollection<DeploymentEnvironmentDto>>;

public sealed class GetDeploymentEnvironmentsQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetDeploymentEnvironmentsQuery, IReadOnlyCollection<DeploymentEnvironmentDto>>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    public async Task<IReadOnlyCollection<DeploymentEnvironmentDto>> Handle(
        GetDeploymentEnvironmentsQuery query, CancellationToken cancellationToken)
    {
        var environments = _productManagementDbContext.DeploymentEnvironments.AsNoTracking();

        if (query.IsActive is not null)
        {
            environments = environments.Where(e => e.IsActive == query.IsActive);
        }

        if (query.Category is not null)
        {
            environments = environments.Where(e => e.Category == query.Category);
        }

        return await environments
            .ProjectToType<DeploymentEnvironmentDto>(Projection(_productManagementDbContext))
            .OrderBy(e => e.RingOrder)
            .ThenBy(e => e.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Maps the environment onto its DTO, adding the one member convention cannot infer.
    /// </summary>
    /// <remarks>
    /// Everything but the count is a same-named property, so Mapster handles it without configuration.
    /// The count is a correlated subquery over a second set, which is why the config takes the context
    /// and cannot be a shared static — the global <c>TypeAdapterConfig</c> is built once at startup and
    /// has no request-scoped DbContext to close over.
    /// </remarks>
    private static TypeAdapterConfig Projection(IProductManagementDbContext dbContext)
    {
        var config = new TypeAdapterConfig();

        config.NewConfig<Domain.Models.DeploymentEnvironment, DeploymentEnvironmentDto>()
            .Map(dto => dto.DeploymentCount, e => dbContext.Deployments.Count(d => d.EnvironmentId == e.Id));

        return config;
    }
}
