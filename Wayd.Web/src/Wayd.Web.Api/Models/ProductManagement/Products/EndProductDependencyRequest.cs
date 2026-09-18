namespace Wayd.Web.Api.Models.ProductManagement.Products;

/// <summary>
/// Records that a product stopped depending on another. The dependency is kept, and still counts for the
/// period it held.
/// </summary>
public sealed record EndProductDependencyRequest
{
    /// <summary>
    /// The last day the dependency held. Defaults to today, may be the day it started, and cannot be in the
    /// future.
    /// </summary>
    public LocalDate? EndsOn { get; set; }
}
