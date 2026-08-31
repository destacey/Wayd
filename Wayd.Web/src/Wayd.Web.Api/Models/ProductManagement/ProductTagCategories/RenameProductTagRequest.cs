namespace Wayd.Web.Api.Models.ProductManagement.ProductTagCategories;

/// <summary>
/// Renames a tag.
/// </summary>
/// <remarks>
/// Safe on a tag already in use: products reference it by id, so the new name shows everywhere at once.
/// </remarks>
public sealed record RenameProductTagRequest
{
    /// <summary>
    /// What the tag is called.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// What the tag means. Cleared when omitted.
    /// </summary>
    public string? Description { get; set; }
}

public sealed class RenameProductTagRequestValidator : CustomValidator<RenameProductTagRequest>
{
    public RenameProductTagRequestValidator()
    {
        RuleFor(t => t.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(t => t.Description)
            .MaximumLength(512);
    }
}
