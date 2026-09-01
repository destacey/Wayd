using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.ProductManagement.Application.Products.Dtos;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Products.Queries;

/// <summary>
/// A single product by id or key, or <c>null</c> when it does not exist.
/// </summary>
/// <remarks>
/// Accepts either so a URL can carry the short integer key a reader can recognise, rather than a GUID,
/// matching how the other modules address a record.
/// </remarks>
public sealed record GetProductQuery : IQuery<ProductDto?>
{
    public GetProductQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Product>();
    }

    public Expression<Func<Product, bool>> IdOrKeyFilter { get; }
}

public sealed class GetProductQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetProductQuery, ProductDto?>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    public async Task<ProductDto?> Handle(GetProductQuery query, CancellationToken cancellationToken)
    {
        return await _productManagementDbContext.Products
            .Where(query.IdOrKeyFilter)
            .ProjectToType<ProductDto>(
                ProductDto.CreateTypeAdapterConfig(_productManagementDbContext))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
