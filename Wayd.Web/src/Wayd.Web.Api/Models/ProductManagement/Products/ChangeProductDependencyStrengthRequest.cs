using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.Web.Api.Models.ProductManagement.Products;

/// <summary>
/// Changes whether a product stops working without one it depends on.
/// </summary>
/// <remarks>
/// Ends the dependency and records a new one with the new strength, so the response is the id of the
/// dependency now open.
/// </remarks>
public sealed record ChangeProductDependencyStrengthRequest
{
    /// <summary>
    /// Whether the product stops working without it (Hard) or carries on without it (Soft).
    /// </summary>
    public DependencyStrength Strength { get; set; }

    /// <summary>
    /// The first day the new strength holds; the current dependency ends the day before. Defaults to today,
    /// must be after the day the dependency started, and cannot be in the future.
    /// </summary>
    public LocalDate? ChangedOn { get; set; }
}

public sealed class ChangeProductDependencyStrengthRequestValidator : CustomValidator<ChangeProductDependencyStrengthRequest>
{
    public ChangeProductDependencyStrengthRequestValidator()
    {
        RuleFor(d => d.Strength)
            .IsInEnum();
    }
}
