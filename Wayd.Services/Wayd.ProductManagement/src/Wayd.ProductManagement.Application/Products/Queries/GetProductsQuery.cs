using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Products.Dtos;

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

        return await Project(products, _productManagementDbContext)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    internal static IQueryable<ProductDto> Project(IQueryable<Domain.Models.Product> products, IProductManagementDbContext dbContext) =>
        products.Select(p => new ProductDto
        {
            Id = p.Id,
            Key = p.Key,
            Name = p.Name,
            Description = p.Description,
            ExternalId = p.ExternalId,
            Type = dbContext.ProductTypes
                .Where(t => t.Id == p.ProductTypeId)
                .Select(t => new NavigationDto
                {
                    Id = t.Id,
                    Key = t.Key,
                    Name = t.Name,
                })
                .FirstOrDefault()!,
            IsReleasable = dbContext.ProductTypes.Where(t => t.Id == p.ProductTypeId).Select(t => t.IsReleasable).FirstOrDefault(),
            Parent = dbContext.Products
                .Where(parent => parent.Id == p.ParentId)
                .Select(parent => new NavigationDto
                {
                    Id = parent.Id,
                    Key = parent.Key,
                    Name = parent.Name,
                })
                .FirstOrDefault(),
            Status = new StatusNavigationDto
            {
                Id = p.StatusId,
                Name = p.StatusName,
                Category = p.StatusCategory,
                Alias = p.StatusAliasValue,
            },
            Tags = p.Tags
                .Select(t => new ProductTagDto
                {
                    TagId = t.TagId,
                    TagName = dbContext.ProductTags.Where(tag => tag.Id == t.TagId).Select(tag => tag.Name).FirstOrDefault()!,
                    CategoryId = t.CategoryId,
                    CategoryName = dbContext.ProductTagCategories.Where(c => c.Id == t.CategoryId).Select(c => c.Name).FirstOrDefault()!,
                })
                .ToList(),
        });
}
