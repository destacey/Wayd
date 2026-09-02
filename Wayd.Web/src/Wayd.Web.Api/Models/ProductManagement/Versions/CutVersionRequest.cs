namespace Wayd.Web.Api.Models.ProductManagement.Versions;

/// <summary>
/// Freezes scope and marks a version ready to ship.
/// </summary>
public sealed record CutVersionRequest
{
    /// <summary>
    /// The date scope was frozen. Supplied rather than taken from the clock, because cutting is often
    /// recorded after the fact.
    /// </summary>
    public LocalDate CutDate { get; set; }
}
