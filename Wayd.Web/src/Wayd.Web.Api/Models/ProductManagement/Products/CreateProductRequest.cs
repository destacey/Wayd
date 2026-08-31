using Wayd.ProductManagement.Application.Products.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.Products;

public sealed record CreateProductRequest
{
    /// <summary>
    /// What the product, component, or service is called.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// What the node is and why it exists.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The node's type, which decides whether releases can be cut against it.
    /// </summary>
    public Guid ProductTypeId { get; set; }

    /// <summary>
    /// The parent node, or null for a root.
    /// </summary>
    public Guid? ParentId { get; set; }

    /// <summary>
    /// The node's identifier in whatever system owns it — a repository, a pipeline, a registry package.
    /// </summary>
    public string? ExternalId { get; set; }

    public CreateProductCommand ToCreateProductCommand() =>
        new(Name, Description, ProductTypeId, ParentId, ExternalId);
}

public sealed class CreateProductRequestValidator : CustomValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(p => p.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(p => p.Description)
            .MaximumLength(1024);

        RuleFor(p => p.ProductTypeId)
            .NotEmpty();

        RuleFor(p => p.ExternalId)
            .MaximumLength(256);
    }
}
