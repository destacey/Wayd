using Wayd.DemandManagement.Application.IntakeRequestTagCategories.Commands;

namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequestTagCategories;

/// <summary>
/// The order the tag axes are presented in.
/// </summary>
public sealed record ReorderIntakeRequestTagCategoriesRequest
{
    /// <summary>
    /// Every axis, inactive ones included, in order. A partial list is refused.
    /// </summary>
    public List<Guid> OrderedCategoryIds { get; set; } = [];

    public ReorderIntakeRequestTagCategoriesCommand ToReorderIntakeRequestTagCategoriesCommand() => new(OrderedCategoryIds);
}

public sealed class ReorderIntakeRequestTagCategoriesRequestValidator : CustomValidator<ReorderIntakeRequestTagCategoriesRequest>
{
    public ReorderIntakeRequestTagCategoriesRequestValidator()
    {
        RuleFor(r => r.OrderedCategoryIds)
            .NotEmpty()
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("The order cannot contain an empty id.");
    }
}
