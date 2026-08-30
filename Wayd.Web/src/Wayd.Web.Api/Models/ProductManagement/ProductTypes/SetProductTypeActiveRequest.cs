using Wayd.ProductManagement.Application.ProductTypes.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.ProductTypes;

/// <summary>
/// Takes a product type out of use, or puts it back.
/// </summary>
/// <remarks>
/// The reversible alternative to deleting: products already using the type keep resolving it.
/// </remarks>
public sealed record SetProductTypeActiveRequest
{
    /// <summary>
    /// The unique identifier of the product type.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Whether new products can be created with this type.
    /// </summary>
    public bool IsActive { get; set; }

    public SetProductTypeActiveCommand ToSetProductTypeActiveCommand() => new(Id, IsActive);
}

public sealed class SetProductTypeActiveRequestValidator : CustomValidator<SetProductTypeActiveRequest>
{
    public SetProductTypeActiveRequestValidator()
    {
        RuleFor(t => t.Id)
            .NotEmpty();
    }
}
