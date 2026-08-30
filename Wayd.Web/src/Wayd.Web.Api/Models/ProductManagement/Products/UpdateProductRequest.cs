using Wayd.ProductManagement.Application.Products.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.Products;

/// <summary>
/// A whole-record update of a product's descriptive fields.
/// </summary>
/// <remarks>
/// PUT semantics: an omitted optional field is cleared, not left as it was. Type, parent and status are
/// changed through their own endpoints, because each carries rules a blanket update could not enforce.
/// </remarks>
public sealed record UpdateProductRequest
{
    /// <summary>
    /// The unique identifier of the product.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// What the product, component, or service is called.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// What the node is and why it exists. Cleared when omitted.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The node's identifier in the system that owns it. Cleared when omitted.
    /// </summary>
    public string? ExternalId { get; set; }

    public UpdateProductDetailsCommand ToUpdateProductDetailsCommand() =>
        new(Id, Name, Description, ExternalId);
}

public sealed class UpdateProductRequestValidator : CustomValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(p => p.Id)
            .NotEmpty();

        RuleFor(p => p.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(p => p.Description)
            .MaximumLength(1024);

        RuleFor(p => p.ExternalId)
            .MaximumLength(256);
    }
}
