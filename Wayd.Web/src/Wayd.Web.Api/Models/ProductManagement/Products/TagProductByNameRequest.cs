using Wayd.Common.Domain.Tagging;
using Wayd.ProductManagement.Application.Products.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.Products;

/// <summary>
/// Tags a product with a typed name.
/// </summary>
public sealed record TagProductByNameRequest
{
    /// <summary>
    /// The tag's name. When exactly one tag on an active category has it, ignoring case, that tag is applied;
    /// otherwise the free tag with the name is applied, and created when none has it yet.
    /// </summary>
    public string Name { get; set; } = default!;

    public TagProductByNameCommand ToTagProductByNameCommand(Guid id) => new(id, Name);
}

public sealed class TagProductByNameRequestValidator : CustomValidator<TagProductByNameRequest>
{
    public TagProductByNameRequestValidator()
    {
        RuleFor(r => r.Name)
            .NotEmpty()
            .MaximumLength(TagBase.NameMaxLength);
    }
}
