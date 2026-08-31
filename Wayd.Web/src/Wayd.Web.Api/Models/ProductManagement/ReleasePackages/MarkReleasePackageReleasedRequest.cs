namespace Wayd.Web.Api.Models.ProductManagement.ReleasePackages;

/// <summary>
/// Records that a package shipped.
/// </summary>
public sealed record MarkReleasePackageReleasedRequest
{
    /// <summary>
    /// The date it shipped. Supplied rather than taken from the clock, because shipping is often
    /// recorded after the fact.
    /// </summary>
    public LocalDate ReleasedDate { get; set; }
}
