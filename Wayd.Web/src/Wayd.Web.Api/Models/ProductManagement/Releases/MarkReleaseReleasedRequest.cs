namespace Wayd.Web.Api.Models.ProductManagement.Releases;

/// <summary>
/// Records that a release shipped.
/// </summary>
public sealed record MarkReleaseReleasedRequest
{
    /// <summary>
    /// The date it shipped. This is what orders a release history, so it is supplied rather than taken
    /// from the clock.
    /// </summary>
    public LocalDate ReleasedDate { get; set; }
}
