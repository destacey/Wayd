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

    public Guid ProductTypeId { get; init; }
    public string ProductTypeName { get; init; } = default!;
    public bool IsReleasable { get; init; }

    public Guid? ParentId { get; init; }
    public string? ParentName { get; init; }

    public Guid StatusId { get; init; }
    public string StatusName { get; init; } = default!;
    public StatusCategory StatusCategory { get; init; }
    public ProductStatusAlias StatusAlias { get; init; }

    public IReadOnlyCollection<ProductTagDto> Tags { get; init; } = [];
}

public sealed record ProductTagDto
{
    public Guid TagId { get; init; }
    public string TagName { get; init; } = default!;
    public Guid CategoryId { get; init; }
    public string CategoryName { get; init; } = default!;
}
