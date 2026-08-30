using Wayd.ProductManagement.Application.Products.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.Products;

/// <summary>
/// Moves a product to another status in the workflow governing it.
/// </summary>
/// <remarks>
/// Takes a status id rather than a named lifecycle action, because statuses are configurable and a fixed
/// set could not reach one an organization invented. The id is validated against the governing workflow.
/// </remarks>
public sealed record ChangeProductStatusRequest
{
    /// <summary>
    /// The unique identifier of the product.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The target status. Must belong to the workflow assigned to products.
    /// </summary>
    public Guid StatusId { get; set; }

    public ChangeProductStatusCommand ToChangeProductStatusCommand() => new(Id, StatusId);
}

public sealed class ChangeProductStatusRequestValidator : CustomValidator<ChangeProductStatusRequest>
{
    public ChangeProductStatusRequestValidator()
    {
        RuleFor(p => p.Id)
            .NotEmpty();

        RuleFor(p => p.StatusId)
            .NotEmpty();
    }
}
