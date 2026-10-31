using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Domain.Models;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.ProductManagement.Application.Products.Dtos;

/// <summary>
/// A product node as a list or detail row.
/// </summary>
/// <remarks>
/// Status is carried as the frozen name, category and alias rather than a workflow lookup, matching how
/// the record stores it: a read never needs the workflow loaded to render or filter a status.
/// </remarks>
public sealed record ProductDto
{
    public Guid Id { get; init; }
    public int Key { get; init; }
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public string? ExternalId { get; init; }

    /// <summary>
    /// The node's type, which decides whether releases can be cut against it.
    /// </summary>
    public NavigationDto Type { get; init; } = default!;

    /// <summary>
    /// Whether releases can be cut against this node.
    /// </summary>
    /// <remarks>
    /// Flattened out of the type rather than left to a second lookup: it is the type's most
    /// consequential consequence, and every reader of a product wants it without resolving the catalog.
    /// </remarks>
    public bool IsReleasable { get; init; }

    /// <summary>
    /// The parent node, or <c>null</c> for a root.
    /// </summary>
    /// <remarks>
    /// A navigation object rather than flat id/name fields, so a caller can link to it the way the rest
    /// of the app does — by key, which is what a URL carries.
    /// </remarks>
    public NavigationDto? Parent { get; init; }

    /// <summary>
    /// The node's current lifecycle status.
    /// </summary>
    public StatusNavigationDto Status { get; init; } = default!;

    public IReadOnlyCollection<ProductTagDto> Tags { get; init; } = [];

    /// <summary>
    /// The teams and teams of teams linked to this product, by name.
    /// </summary>
    /// <remarks>
    /// Only the product's own links; a team linked to a product beneath it is not listed here. The product's
    /// teams endpoint rolls those up.
    /// </remarks>
    public IReadOnlyCollection<ProductTeamNavigationDto> Teams { get; init; } = [];

    /// <summary>
    /// Maps a product and its tag assignments, for <c>ProjectToType</c>.
    /// </summary>
    /// <remarks>
    /// Built per call rather than registered globally, because the category name reads a second set and
    /// the global config has no request-scoped DbContext to close over.
    /// <para>
    /// <see cref="ProductDto.Parent"/> needs no entry — it is same-named, so convention pairs it with
    /// the navigation. <see cref="ProductDto.Type"/> does, because the entity calls it
    /// <c>ProductType</c>. <see cref="ProductDto.IsReleasable"/> is flattened off that type rather than
    /// the product, which carries no such member.
    /// </para>
    /// <para>
    /// A tag's category is reached through the tag: <c>CategoryId</c> is denormalized onto the
    /// assignment so filtering by axis needs no join, but it carries no foreign key, so there is no
    /// navigation to traverse.
    /// </para>
    /// </remarks>
    public static TypeAdapterConfig CreateTypeAdapterConfig(IProductManagementDbContext dbContext)
    {
        var config = new TypeAdapterConfig();

        config.NewConfig<ProductTagAssignment, ProductTagDto>()
            .Map(dto => dto.TagName, a => a.Tag!.Name)
            .Map(dto => dto.CategoryName, a => dbContext.ProductTagCategories
                .Where(c => c.Id == a.CategoryId)
                .Select(c => c.Name)
                .FirstOrDefault());

        config.NewConfig<ProductTeamLink, ProductTeamNavigationDto>()
            .Map(dto => dto.Id, l => l.TeamId)
            .Map(dto => dto.Key, l => l.Team!.Key)
            .Map(dto => dto.Name, l => l.Team!.Name)
            .Map(dto => dto.Code, l => l.Team!.Code.Value)
            .Map(dto => dto.Type, l => l.Team!.Type)
            .Map(dto => dto.IsActive, l => l.Team!.IsActive);

        config.NewConfig<Product, ProductDto>()
            .Map(dto => dto.Teams, p => p.TeamLinks.OrderBy(l => l.Team!.Name).ThenBy(l => l.TeamId))
            .Map(dto => dto.Type, p => NavigationDto.Create(p.ProductType!.Id, p.ProductType.Key, p.ProductType.Name))
            .Map(dto => dto.IsReleasable, p => p.ProductType!.IsReleasable)
            .Map(dto => dto.Status, p => new StatusNavigationDto
            {
                Id = p.StatusId,
                Name = p.StatusName,
                Category = p.StatusCategory,
                Alias = p.StatusAliasValue,
            });

        return config;
    }
}

public sealed record ProductTagDto
{
    public Guid TagId { get; init; }
    public string TagName { get; init; } = default!;

    /// <summary>The tag's axis, or null for a free tag.</summary>
    public Guid? CategoryId { get; init; }

    /// <summary>The axis's name, or null for a free tag.</summary>
    public string? CategoryName { get; init; }
}
