using Wayd.ProductManagement.Application.Products.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.Products;

public sealed record ReparentProductRequest
{
    /// <summary>
    /// The unique identifier of the product being moved.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The new parent, or null to make this a root node.
    /// </summary>
    public Guid? ParentId { get; set; }

    public ReparentProductCommand ToReparentProductCommand() => new(Id, ParentId);
}

public sealed class ReparentProductRequestValidator : CustomValidator<ReparentProductRequest>
{
    public ReparentProductRequestValidator()
    {
        RuleFor(p => p.Id)
            .NotEmpty();

        RuleFor(p => p.ParentId)
            .NotEqual(p => p.Id)
            .WithMessage("A product cannot be its own parent.");
    }
}
