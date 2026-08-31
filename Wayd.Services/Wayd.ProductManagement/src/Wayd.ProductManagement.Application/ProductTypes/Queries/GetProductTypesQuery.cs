using Wayd.ProductManagement.Application.ProductTypes.Dtos;

namespace Wayd.ProductManagement.Application.ProductTypes.Queries;

/// <summary>
/// The product type catalog.
/// </summary>
/// <param name="isActive">
/// Narrows to active or inactive types. Null returns both — a settings screen manages what a picker
/// would hide.
/// </param>
public sealed record GetProductTypesQuery(bool? IsActive = null) : IQuery<IReadOnlyCollection<ProductTypeDto>>;

public sealed class GetProductTypesQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetProductTypesQuery, IReadOnlyCollection<ProductTypeDto>>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    public async Task<IReadOnlyCollection<ProductTypeDto>> Handle(
        GetProductTypesQuery query, CancellationToken cancellationToken)
    {
        var types = _productManagementDbContext.ProductTypes.AsNoTracking();

        if (query.IsActive is not null)
        {
            types = types.Where(t => t.IsActive == query.IsActive);
        }

        return await types
            .Select(t => new ProductTypeDto
            {
                Id = t.Id,
                Key = t.Key,
                Name = t.Name,
                Description = t.Description,
                IsReleasable = t.IsReleasable,
                Order = t.Order,
                IsActive = t.IsActive,
                IsSystem = t.IsSystem,
                ProductCount = _productManagementDbContext.Products.Count(p => p.ProductTypeId == t.Id),
            })
            .OrderBy(t => t.Order)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }
}
