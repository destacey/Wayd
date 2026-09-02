using Wayd.ProductManagement.Application.ProductTagCategories.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.ProductTagCategories;

/// <summary>
/// Puts the tag axes in a given order.
/// </summary>
public sealed record ReorderProductTagCategoriesRequest
{
    /// <summary>
    /// Every tag category, in the order they should be presented. Must name the whole set: a partial
    /// list would leave the categories it omits at stale positions.
    /// </summary>
    public List<Guid> OrderedCategoryIds { get; set; } = [];

    public ReorderProductTagCategoriesCommand ToReorderProductTagCategoriesCommand() =>
        new(OrderedCategoryIds);
}

public sealed class ReorderProductTagCategoriesRequestValidator
    : CustomValidator<ReorderProductTagCategoriesRequest>
{
    public ReorderProductTagCategoriesRequestValidator()
    {
        RuleFor(c => c.OrderedCategoryIds)
            .NotEmpty()
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("OrderedCategoryIds cannot contain empty GUIDs.");
    }
}
