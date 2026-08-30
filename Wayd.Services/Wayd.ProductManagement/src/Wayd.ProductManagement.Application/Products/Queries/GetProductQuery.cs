using Wayd.ProductManagement.Application.Products.Dtos;

namespace Wayd.ProductManagement.Application.Products.Queries;

/// <summary>
/// A single product node by id, or <c>null</c> when it does not exist.
/// </summary>
public sealed record GetProductQuery(Guid Id) : IQuery<ProductDto?>;

public sealed class GetProductQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetProductQuery, ProductDto?>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    public async Task<ProductDto?> Handle(GetProductQuery query, CancellationToken cancellationToken)
    {
        var products = _productManagementDbContext.Products
            .AsNoTracking()
            .Where(p => p.Id == query.Id);

        return await GetProductsQueryHandler
            .Project(products, _productManagementDbContext)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
