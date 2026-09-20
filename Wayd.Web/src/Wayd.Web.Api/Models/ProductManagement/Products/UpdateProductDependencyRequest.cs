using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.Web.Api.Models.ProductManagement.Products;

/// <summary>
/// Rewords what a product's dependency is for, and records the styles it uses where none were recorded.
/// </summary>
public sealed record UpdateProductDependencyRequest
{
    /// <summary>
    /// What the dependency is for, or null to clear it.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// How the product reaches the one it depends on, where nobody has recorded it yet. Omitting this leaves
    /// recorded styles alone rather than clearing them — unlike the description, which an omitted value
    /// clears. Changing styles already recorded is refused: that is a change of terms, which has to be dated.
    /// </summary>
    public IReadOnlyCollection<InteractionStyle>? InteractionStyles { get; set; }
}

public sealed class UpdateProductDependencyRequestValidator : CustomValidator<UpdateProductDependencyRequest>
{
    public UpdateProductDependencyRequestValidator()
    {
        RuleFor(d => d.Description)
            .MaximumLength(1024);

        // Each entry names one style. IsInEnum would accept a combination, since on a flags enum it tests
        // the bits rather than the declared members.
        RuleForEach(d => d.InteractionStyles)
            .Must(Enum.IsDefined)
            .WithMessage("'{PropertyValue}' is not an interaction style.");
    }
}
