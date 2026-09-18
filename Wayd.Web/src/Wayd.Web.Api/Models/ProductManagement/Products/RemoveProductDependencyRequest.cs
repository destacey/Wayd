namespace Wayd.Web.Api.Models.ProductManagement.Products;

/// <summary>
/// Deletes a dependency that was recorded by mistake.
/// </summary>
/// <remarks>
/// Not for a dependency that stopped — ending it keeps the history of when it held.
/// </remarks>
public sealed record RemoveProductDependencyRequest
{
    /// <summary>
    /// Why the dependency was never true. Required.
    /// </summary>
    public string Reason { get; set; } = default!;
}

public sealed class RemoveProductDependencyRequestValidator : CustomValidator<RemoveProductDependencyRequest>
{
    public RemoveProductDependencyRequestValidator()
    {
        RuleFor(d => d.Reason)
            .NotEmpty()
            .MaximumLength(1024);
    }
}
