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
    /// How the product reaches the one it depends on — both where it calls it and subscribes to it. Omit or
    /// send an empty list to record none, which is not the same as recording that there are none.
    /// </summary>
    public IReadOnlyCollection<InteractionStyle>? InteractionStyles { get; set; }

    /// <summary>
    /// What the dependency is for.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The day the dependency began. Defaults to today, may be backdated, and cannot be in the future.
    /// </summary>
    public LocalDate? StartsOn { get; set; }

    public AddProductDependencyCommand ToAddProductDependencyCommand(Guid productId)
        => new(productId, DependsOnProductId, Strength, InteractionStyles, Description, StartsOn);
}

public sealed class AddProductDependencyRequestValidator : CustomValidator<AddProductDependencyRequest>
{
    public AddProductDependencyRequestValidator()
    {
        RuleFor(d => d.DependsOnProductId)
            .NotEmpty();

        RuleFor(d => d.Strength)
            .IsInEnum();

        // Each entry names one style. IsInEnum would accept a combination, since on a flags enum it tests
        // the bits rather than the declared members.
        RuleForEach(d => d.InteractionStyles)
            .Must(Enum.IsDefined)
            .WithMessage("'{PropertyValue}' is not an interaction style.");

        RuleFor(d => d.Description)
            .MaximumLength(1024);
    }
}
