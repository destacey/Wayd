using Wayd.ProductManagement.Application.Products.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.Products;

/// <summary>
/// A whole-record update of a product's descriptive fields.
/// </summary>
/// <remarks>
/// PUT semantics: an omitted optional field is cleared, not left as it was. Type, parent, status and
/// the external link are changed through their own endpoints — each carries rules a blanket update
/// could not enforce, and the link would otherwise have to be restated by every rename.
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

    public UpdateProductDetailsCommand ToUpdateProductDetailsCommand() =>
        new(Id, Name, Description);
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
    }
}
