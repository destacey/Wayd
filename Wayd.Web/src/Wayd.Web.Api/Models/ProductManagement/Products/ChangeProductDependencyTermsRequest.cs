using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.Products.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.Products;

/// <summary>
/// Changes the terms a product's dependency holds on — whether the product stops working without the one it
/// depends on, how it reaches it, or both.
/// </summary>
/// <remarks>
/// Ends the dependency and records a new one on the new terms, so the response is the id of the dependency
/// now open. Recording styles on a dependency that had none is the exception: it fills them in place and
/// returns the same id, because nothing about the dependency changed.
/// </remarks>
public sealed record ChangeProductDependencyTermsRequest
{
    /// <summary>
    /// Whether the product stops working without it (Hard) or carries on without it (Soft).
    /// </summary>
    public DependencyStrength Strength { get; set; }

    /// <summary>
    /// How the product reaches the one it depends on. Omit or send an empty list to carry the recorded
    /// styles onto the new dependency rather than clearing them.
    /// </summary>
    public IReadOnlyCollection<InteractionStyle>? InteractionStyles { get; set; }

    /// <summary>
    /// The first day the new terms hold; the current dependency ends the day before. Defaults to today,
    /// must be after the day the dependency started, and cannot be in the future.
    /// </summary>
    public LocalDate? ChangedOn { get; set; }

    public ChangeProductDependencyTermsCommand ToChangeProductDependencyTermsCommand(Guid productId, Guid dependencyId)
        => new(productId, dependencyId, Strength, InteractionStyles, ChangedOn);
}

public sealed class ChangeProductDependencyTermsRequestValidator : CustomValidator<ChangeProductDependencyTermsRequest>
{
    public ChangeProductDependencyTermsRequestValidator()
    {
        RuleFor(d => d.Strength)
            .IsInEnum();

        // Each entry names one style. IsInEnum would accept a combination, since on a flags enum it tests
        // the bits rather than the declared members.
        RuleForEach(d => d.InteractionStyles)
            .Must(Enum.IsDefined)
            .WithMessage("'{PropertyValue}' is not an interaction style.");
    }
}
