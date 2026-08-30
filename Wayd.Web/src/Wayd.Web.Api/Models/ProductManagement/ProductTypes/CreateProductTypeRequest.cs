using Wayd.ProductManagement.Application.ProductTypes.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.ProductTypes;

public sealed record CreateProductTypeRequest
{
    /// <summary>
    /// What this kind of node is called — Application, Service, Library.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// What the type is for, shown when choosing one.
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

    public CreateProductTypeCommand ToCreateProductTypeCommand() =>
        new(Name, Description, IsReleasable, Order);
}

public sealed class CreateProductTypeRequestValidator : CustomValidator<CreateProductTypeRequest>
{
    public CreateProductTypeRequestValidator()
    {
        RuleFor(t => t.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(t => t.Description)
            .MaximumLength(512);

        RuleFor(t => t.Order)
            .GreaterThanOrEqualTo(0);
    }
}
