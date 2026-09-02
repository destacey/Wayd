namespace Wayd.Web.Api.Models.ProductManagement.Versions;

/// <summary>
/// Records that a version shipped.
/// </summary>
public sealed record MarkVersionReleasedRequest
{
    /// <summary>
    /// The date it shipped. This is what orders a version history, so it is supplied rather than taken
    /// from the clock.
    /// </summary>
    public LocalDate ReleasedDate { get; set; }
}
