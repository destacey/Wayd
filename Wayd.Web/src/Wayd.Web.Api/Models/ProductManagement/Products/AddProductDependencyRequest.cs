using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.Products.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.Products;

/// <summary>
/// Records that a product depends on another.
/// </summary>
public sealed record AddProductDependencyRequest
{
    /// <summary>
    /// The product depended on. Cannot be the product itself, nor anything above or below it in the tree.
    /// </summary>
    public Guid DependsOnProductId { get; set; }

    /// <summary>
    /// Whether the product stops working without it (Hard) or carries on without it (Soft).
    /// </summary>
    public DependencyStrength Strength { get; set; }

    /// <summary>
    /// What the dependency is for.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The day the dependency began. Defaults to today, may be backdated, and cannot be in the future.
    /// </summary>
    public LocalDate? StartsOn { get; set; }

    public AddProductDependencyCommand ToAddProductDependencyCommand(Guid productId)
        => new(productId, DependsOnProductId, Strength, Description, StartsOn);
}

public sealed class AddProductDependencyRequestValidator : CustomValidator<AddProductDependencyRequest>
{
    public AddProductDependencyRequestValidator()
    {
        RuleFor(d => d.DependsOnProductId)
            .NotEmpty();

        RuleFor(d => d.Strength)
            .IsInEnum();

        RuleFor(d => d.Description)
            .MaximumLength(1024);
    }
}
