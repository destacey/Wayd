using Wayd.ProductManagement.Application.ProductTypes.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.ProductTypes;

/// <summary>
/// A whole-record update of a product type.
/// </summary>
/// <remarks>
/// Turning <see cref="IsReleasable"/> off refuses <em>new</em> releases only; those already cut stand as
/// historical records.
/// </remarks>
public sealed record UpdateProductTypeRequest
{
    /// <summary>
    /// The unique identifier of the product type.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// What this kind of node is called.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// What the type is for. Cleared when omitted.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether releases can be cut against nodes of this type.
    /// </summary>
    public bool IsReleasable { get; set; }

    /// <summary>
    /// Display position when presenting the catalog. Presentation only.
    /// </summary>
    public int Order { get; set; }

    public UpdateProductTypeCommand ToUpdateProductTypeCommand() =>
        new(Id, Name, Description, IsReleasable, Order);
}

public sealed class UpdateProductTypeRequestValidator : CustomValidator<UpdateProductTypeRequest>
{
    public UpdateProductTypeRequestValidator()
    {
        RuleFor(t => t.Id)
            .NotEmpty();

        RuleFor(t => t.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(t => t.Description)
            .MaximumLength(512);

        RuleFor(t => t.Order)
            .GreaterThanOrEqualTo(0);
    }
}
