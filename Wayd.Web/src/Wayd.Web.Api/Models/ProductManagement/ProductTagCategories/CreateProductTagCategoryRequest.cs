using Wayd.ProductManagement.Application.ProductTagCategories.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.ProductTagCategories;

public sealed record CreateProductTagCategoryRequest
{
    /// <summary>
    /// What the organization calls this axis — Platform, Tech Stack, Compliance.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// What the axis is for, shown when choosing tags.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether a product can carry several tags from this axis. True suits Platform, where a
    /// cross-platform app genuinely targets both iOS and Android. <strong>Fixed once set</strong>:
    /// narrowing it later would leave products holding more tags than the axis permits.
    /// </summary>
    public bool AllowsMany { get; set; }

    /// <summary>
    /// Display position when presenting the axes. Presentation only.
    /// </summary>
    public int Order { get; set; }

    public CreateProductTagCategoryCommand ToCreateProductTagCategoryCommand() =>
        new(Name, Description, AllowsMany, Order);
}

public sealed class CreateProductTagCategoryRequestValidator : CustomValidator<CreateProductTagCategoryRequest>
{
    public CreateProductTagCategoryRequestValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(c => c.Description)
            .MaximumLength(512);

        RuleFor(c => c.Order)
            .GreaterThanOrEqualTo(0);
    }
}
