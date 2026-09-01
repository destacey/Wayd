using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Products.Dtos;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Products.Queries;

/// <summary>
/// Every product node, optionally narrowed.
/// </summary>
/// <param name="StatusCategories">
/// Category buckets to include. Empty returns every status — the caller filters, this does not assume
/// retired nodes are unwanted.
/// </param>
public sealed record GetProductsQuery(
    Guid? ParentId = null,
    Guid? ProductTypeId = null,
    IReadOnlyCollection<StatusCategory>? StatusCategories = null,
    IReadOnlyCollection<Guid>? TagIds = null) : IQuery<IReadOnlyCollection<ProductDto>>;

public sealed class GetProductsQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetProductsQuery, IReadOnlyCollection<ProductDto>>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    public async Task<IReadOnlyCollection<ProductDto>> Handle(GetProductsQuery query, CancellationToken cancellationToken)
    {
        var products = _productManagementDbContext.Products.AsNoTracking();

        if (query.ParentId is not null)
        {
            products = products.Where(p => p.ParentId == query.ParentId);
        }

        if (query.ProductTypeId is not null)
        {
            products = products.Where(p => p.ProductTypeId == query.ProductTypeId);
        }

        if (query.StatusCategories is { Count: > 0 })
        {
            products = products.Where(p => query.StatusCategories.Contains(p.StatusCategory));
        }

        if (query.TagIds is { Count: > 0 })
        {
            // Every tag, not any: narrowing by Platform and Compliance together means both hold.
            foreach (var tagId in query.TagIds)
            {
                var required = tagId;
                products = products.Where(p => p.Tags.Any(t => t.TagId == required));
            }
        }

        return await products
            .ProjectToType<ProductDto>(
                ProductDto.CreateTypeAdapterConfig(_productManagementDbContext))
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }
}
