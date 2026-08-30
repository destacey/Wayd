using Wayd.ProductManagement.Application.ProductTagCategories.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.ProductTagCategories;

/// <summary>
/// Edits a tag axis.
/// </summary>
/// <remarks>
/// <c>AllowsMany</c> is deliberately absent: narrowing an axis from many to one would leave products
/// holding more tags than it permits, and nothing can choose which to drop.
/// </remarks>
public sealed record UpdateProductTagCategoryRequest
{
    /// <summary>
    /// The unique identifier of the tag category.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// What the organization calls this axis.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// What the axis is for. Cleared when omitted.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Display position when presenting the axes. Presentation only.
    /// </summary>
    public int Order { get; set; }

    public UpdateProductTagCategoryCommand ToUpdateProductTagCategoryCommand() =>
        new(Id, Name, Description, Order);
}

public sealed class UpdateProductTagCategoryRequestValidator : CustomValidator<UpdateProductTagCategoryRequest>
{
    public UpdateProductTagCategoryRequestValidator()
    {
        RuleFor(c => c.Id)
            .NotEmpty();

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(c => c.Description)
            .MaximumLength(512);

        RuleFor(c => c.Order)
            .GreaterThanOrEqualTo(0);
    }
}
