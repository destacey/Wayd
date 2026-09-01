using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.ProductTypes.Dtos;

/// <summary>
/// A product type as a catalog row.
/// </summary>
public sealed record ProductTypeDto
{
    public Guid Id { get; init; }
    public int Key { get; init; }
    public string Name { get; init; } = default!;
    public string? Description { get; init; }

    /// <summary>Whether releases can be cut against nodes of this type.</summary>
    public bool IsReleasable { get; init; }

    /// <summary>Display position when presenting the catalog. Presentation only.</summary>
    public int Order { get; init; }

    /// <summary>Whether new products can still be created with this type.</summary>
    public bool IsActive { get; init; }

    /// <summary>Platform-seeded types are read-only: deactivate rather than delete or edit.</summary>
    public bool IsSystem { get; init; }

    /// <summary>
    /// How many products currently carry this type. Lets a caller see what deactivating would affect.
    /// </summary>
    public int ProductCount { get; init; }

    /// <summary>
    /// Maps the type, for <c>ProjectToType</c>. Everything but the count is same-named.
    /// </summary>
    /// <remarks>
    /// Built per call rather than registered globally, because the count reads a second set and the
    /// global config has no request-scoped DbContext to close over.
    /// </remarks>
    public static TypeAdapterConfig CreateTypeAdapterConfig(IProductManagementDbContext dbContext)
    {
        var config = new TypeAdapterConfig();

        config.NewConfig<ProductType, ProductTypeDto>()
            .Map(dto => dto.ProductCount, t => dbContext.Products.Count(p => p.ProductTypeId == t.Id));

        return config;
    }
}
