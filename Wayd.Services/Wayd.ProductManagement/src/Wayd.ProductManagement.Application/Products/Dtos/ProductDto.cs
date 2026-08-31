using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.Enums.ProductManagement;
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
}

public sealed record ProductTagDto
{
    public Guid TagId { get; init; }
    public string TagName { get; init; } = default!;
    public Guid CategoryId { get; init; }
    public string CategoryName { get; init; } = default!;
}
