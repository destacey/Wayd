using Wayd.ProductManagement.Application.ProductTagCategories.Dtos;

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
        var categories = _productManagementDbContext.ProductTagCategories.AsNoTracking();

        if (query.IsActive is not null)
        {
            categories = categories.Where(c => c.IsActive == query.IsActive);
        }

        return await categories
            .Select(c => new ProductTagCategoryDto
            {
                Id = c.Id,
                Key = c.Key,
                Name = c.Name,
                Description = c.Description,
                AllowsMany = c.AllowsMany,
                Order = c.Order,
                IsActive = c.IsActive,
                IsSystem = c.IsSystem,
                Tags = _productManagementDbContext.ProductTags
                    .Where(t => t.CategoryId == c.Id)
                    .OrderBy(t => t.Order)
                    .Select(t => new ProductTagOptionDto
                    {
                        Id = t.Id,
                        Name = t.Name,
                        Description = t.Description,
                        Order = t.Order,
                        IsActive = t.IsActive,
                        ProductCount = _productManagementDbContext.ProductTagAssignments.Count(a => a.TagId == t.Id),
                    })
                    .ToList(),
            })
            .OrderBy(c => c.Order)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }
}
