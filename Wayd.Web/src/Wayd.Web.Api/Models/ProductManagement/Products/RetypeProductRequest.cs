using Wayd.ProductManagement.Application.Products.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.Products;

public sealed record RetypeProductRequest
{
    /// <summary>
    /// The unique identifier of the product.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The type to move the product onto.
    /// </summary>
    public Guid ProductTypeId { get; set; }

    public RetypeProductCommand ToRetypeProductCommand() => new(Id, ProductTypeId);
}

public sealed class RetypeProductRequestValidator : CustomValidator<RetypeProductRequest>
{
    public RetypeProductRequestValidator()
    {
        RuleFor(p => p.Id)
            .NotEmpty();

        RuleFor(p => p.ProductTypeId)
            .NotEmpty();
    }
}
