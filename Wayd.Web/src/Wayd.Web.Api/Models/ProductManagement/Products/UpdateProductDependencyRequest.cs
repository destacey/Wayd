namespace Wayd.Web.Api.Models.ProductManagement.Products;

/// <summary>
/// Rewords what a product's dependency is for.
/// </summary>
public sealed record UpdateProductDependencyRequest
{
    /// <summary>
    /// What the dependency is for, or null to clear it.
    /// </summary>
    public string? Description { get; set; }
}

public sealed class UpdateProductDependencyRequestValidator : CustomValidator<UpdateProductDependencyRequest>
{
    public UpdateProductDependencyRequestValidator()
    {
        RuleFor(d => d.Description)
            .MaximumLength(1024);
    }
}
