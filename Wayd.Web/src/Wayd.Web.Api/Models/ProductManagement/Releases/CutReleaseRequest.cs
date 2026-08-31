namespace Wayd.Web.Api.Models.ProductManagement.Releases;

/// <summary>
/// Freezes scope and marks a release ready to ship.
/// </summary>
public sealed record CutReleaseRequest
{
    /// <summary>
    /// The date scope was frozen. Supplied rather than taken from the clock, because cutting is often
    /// recorded after the fact.
    /// </summary>
    public LocalDate CutDate { get; set; }
}
