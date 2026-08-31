using Wayd.ProductManagement.Application.ProductTagCategories.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.ProductTagCategories;

/// <summary>
/// Takes a tag axis out of use, or puts it back. Products already tagged keep their tags.
/// </summary>
public sealed record SetProductTagCategoryActiveRequest
{
    /// <summary>
    /// The unique identifier of the tag category.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Whether products can still be tagged along this axis.
    /// </summary>
    public bool IsActive { get; set; }

    public SetProductTagCategoryActiveCommand ToSetProductTagCategoryActiveCommand() => new(Id, IsActive);
}

public sealed class SetProductTagCategoryActiveRequestValidator
    : CustomValidator<SetProductTagCategoryActiveRequest>
{
    public SetProductTagCategoryActiveRequestValidator()
    {
        RuleFor(c => c.Id)
            .NotEmpty();
    }
}
