namespace Wayd.Web.Api.Models.ProductManagement.ProductTagCategories;

/// <summary>
/// Retires a tag from new use, or puts it back. Products already carrying it keep it.
/// </summary>
public sealed record SetProductTagActiveRequest
{
    /// <summary>
    /// Whether products can still be tagged with this.
    /// </summary>
    public bool IsActive { get; set; }
}
