using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.ProductTagCategories.Dtos;

/// <summary>
/// A tag axis and the tags on it.
/// </summary>
public sealed record ProductTagCategoryDto
{
    public Guid Id { get; init; }
    public int Key { get; init; }
    public string Name { get; init; } = default!;
    public string? Description { get; init; }

    /// <summary>Whether a product can carry several tags from this axis.</summary>
    public bool AllowsMany { get; init; }

    /// <summary>Display position when presenting the axes. Presentation only.</summary>
    public int Order { get; init; }

    /// <summary>Whether products can still be tagged along this axis.</summary>
    public bool IsActive { get; init; }

    /// <summary>Platform-seeded axes are read-only: they cannot be edited or deleted.</summary>
    public bool IsSystem { get; init; }

    public IReadOnlyCollection<ProductTagOptionDto> Tags { get; init; } = [];

    /// <summary>
    /// Maps a category and its tags, for <c>ProjectToType</c>.
    /// </summary>
    /// <remarks>
    /// Built per call rather than registered globally, because both members below read a second set
    /// and the global config has no request-scoped DbContext to close over.
    /// <para>
    /// <see cref="ProductTagCategory.Tags"/> is a real navigation but cannot serve this: its getter
    /// applies the <c>Order</c> sort in the property body, which EF projects around rather than
    /// translates, so taking it would return the tags unordered.
    /// </para>
    /// </remarks>
    public static TypeAdapterConfig CreateTypeAdapterConfig(IProductManagementDbContext dbContext)
    {
        var config = new TypeAdapterConfig();

        config.NewConfig<ProductTag, ProductTagOptionDto>()
            .Map(dto => dto.ProductCount, t => dbContext.ProductTagAssignments.Count(a => a.TagId == t.Id));

        config.NewConfig<ProductTagCategory, ProductTagCategoryDto>()
            .Map(dto => dto.Tags, c => dbContext.ProductTags
                .Where(t => t.CategoryId == c.Id)
                .OrderBy(t => t.Order));

        return config;
    }
}

public sealed record ProductTagOptionDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public int Order { get; init; }
    public bool IsActive { get; init; }

    /// <summary>
    /// How many products currently carry this tag, so a caller can see what deactivating would affect.
    /// </summary>
    public int ProductCount { get; init; }
}
