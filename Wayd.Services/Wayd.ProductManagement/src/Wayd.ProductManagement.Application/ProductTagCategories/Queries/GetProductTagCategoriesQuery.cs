using Wayd.ProductManagement.Application.ProductTagCategories.Dtos;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.ProductTagCategories.Queries;

/// <summary>
/// The tag axes and their tags.
/// </summary>
/// <param name="IsActive">
/// Narrows to active or inactive axes. Null returns both — a settings screen manages what a picker
/// would hide.
/// </param>
public sealed record GetProductTagCategoriesQuery(bool? IsActive = null)
    : IQuery<IReadOnlyCollection<ProductTagCategoryDto>>;

public sealed class GetProductTagCategoriesQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetProductTagCategoriesQuery, IReadOnlyCollection<ProductTagCategoryDto>>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    public async Task<IReadOnlyCollection<ProductTagCategoryDto>> Handle(
        GetProductTagCategoriesQuery query, CancellationToken cancellationToken)
    {
        var categories = _productManagementDbContext.ProductTagCategories.AsQueryable();

        if (query.IsActive is not null)
        {
            categories = categories.Where(c => c.IsActive == query.IsActive);
        }

        return await categories
            .ProjectToType<ProductTagCategoryDto>(
                ProductTagCategoryDto.CreateTypeAdapterConfig(_productManagementDbContext))
            .OrderBy(c => c.Order)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }
}
