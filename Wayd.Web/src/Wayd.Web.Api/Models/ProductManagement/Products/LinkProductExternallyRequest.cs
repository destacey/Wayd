using Wayd.ProductManagement.Application.Products.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.Products;

/// <summary>
/// Points a product at the record that owns it in another system, or clears the link.
/// </summary>
public sealed record LinkProductExternallyRequest
{
    /// <summary>
    /// The unique identifier of the product.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The node's identifier in whatever system owns it — a repository, a pipeline, a registry
    /// package. Omit it to unlink.
    /// </summary>
    public string? ExternalId { get; set; }

    public LinkProductExternallyCommand ToLinkProductExternallyCommand() => new(Id, ExternalId);
}

public sealed class LinkProductExternallyRequestValidator : CustomValidator<LinkProductExternallyRequest>
{
    public LinkProductExternallyRequestValidator()
    {
        RuleFor(p => p.Id)
            .NotEmpty();

        RuleFor(p => p.ExternalId)
            .MaximumLength(256);
    }
}
