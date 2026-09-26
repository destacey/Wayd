namespace Wayd.Web.Api.Models.ProductManagement.Versions;

/// <summary>
/// Freezes scope and marks a version ready to ship.
/// </summary>
public sealed record CutVersionRequest
{
    /// <summary>
    /// The moment scope was frozen — the build or tag that cut it. Supplied rather than taken from the
    /// clock, because cutting is often recorded after the fact.
    /// </summary>
    public Instant CutAt { get; set; }
}
