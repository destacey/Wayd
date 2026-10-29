using Wayd.ProductManagement.Application.ProductTags.Dtos;

namespace Wayd.ProductManagement.Application.ProductTags.Queries;

/// <summary>
/// Product tags, on axes and free, sorted by name. Serves both the type-ahead while tagging and the list of free
/// tags an admin manages.
/// </summary>
/// <param name="Search">Keeps tags whose name contains this, ignoring case.</param>
/// <param name="IsFree">True for free tags only, false for tags on axes only, null for both.</param>
/// <param name="IncludeInactive">
/// Whether to keep inactive tags and tags on inactive axes, which cannot be applied. Off for a picker.
/// </param>
public sealed record GetProductTagsQuery(string? Search = null, bool? IsFree = null, bool IncludeInactive = false)
    : IQuery<IReadOnlyList<ProductTagListItemDto>>;

public sealed class GetProductTagsQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetProductTagsQuery, IReadOnlyList<ProductTagListItemDto>>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    public async Task<IReadOnlyList<ProductTagListItemDto>> Handle(GetProductTagsQuery request, CancellationToken cancellationToken)
    {
        var tags = _productManagementDbContext.ProductTags.AsQueryable();

        if (request.IsFree is { } isFree)
            tags = isFree ? tags.Where(t => t.CategoryId == null) : tags.Where(t => t.CategoryId != null);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            tags = tags.Where(t => t.Name.ToLower().Contains(search));
        }

        var rows = await tags
            .Select(t => new
            {
                Tag = t,
                Category = _productManagementDbContext.ProductTagCategories.FirstOrDefault(c => c.Id == t.CategoryId),
                ProductCount = _productManagementDbContext.ProductTagAssignments.Count(a => a.TagId == t.Id),
            })
            .Where(r => request.IncludeInactive || (r.Tag.IsActive && (r.Category == null || r.Category.IsActive)))
            .Select(r => new ProductTagListItemDto
            {
                Id = r.Tag.Id,
                Name = r.Tag.Name,
                Description = r.Tag.Description,
                CategoryId = r.Tag.CategoryId,
                CategoryName = r.Category == null ? null : r.Category.Name,
                IsFree = r.Tag.CategoryId == null,
                IsActive = r.Tag.IsActive,
                ProductCount = r.ProductCount,
            })
            .ToListAsync(cancellationToken);

        return [.. rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.CategoryName, StringComparer.OrdinalIgnoreCase)];
    }
}
