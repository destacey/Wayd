namespace Wayd.Web.Api.Models.ProductManagement.ProductTagCategories;

/// <summary>
/// Adds a tag to an axis.
/// </summary>
public sealed record AddProductTagRequest
{
    /// <summary>
    /// What the tag is called — ios, android, pci-scope.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// What the tag means, shown when choosing it.
    /// </summary>
    public string? Description { get; set; }
}

public sealed class AddProductTagRequestValidator : CustomValidator<AddProductTagRequest>
{
    public AddProductTagRequestValidator()
    {
        RuleFor(t => t.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(t => t.Description)
            .MaximumLength(512);
    }
}
