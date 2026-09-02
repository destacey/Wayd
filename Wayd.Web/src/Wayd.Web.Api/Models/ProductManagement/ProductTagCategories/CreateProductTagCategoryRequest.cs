using Wayd.ProductManagement.Application.ProductTagCategories.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.ProductTagCategories;

/// <remarks>
/// A new axis lands at the end of the list — its position is set by reordering, not on creation.
/// </remarks>
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

    public CreateProductTagCategoryCommand ToCreateProductTagCategoryCommand() =>
        new(Name, Description, AllowsMany);
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
    }
}
