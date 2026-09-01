namespace Wayd.Web.Api.Models.ProductManagement.Releases;

/// <summary>
/// Corrects a release's recorded cut and released dates.
/// </summary>
/// <remarks>
/// Both dates are sent together, because the rule that the released date cannot precede the cut date
/// spans the pair and a correction commonly moves both.
/// </remarks>
public sealed record CorrectReleaseDatesRequest
{
    /// <summary>
    /// The corrected cut date, or null if the release has not been cut. A release that has been cut
    /// cannot have this cleared, and one that has not cannot have it added — cutting is its own action.
    /// </summary>
    public LocalDate? CutDate { get; set; }

    /// <summary>
    /// The corrected released date, or null if the release has not shipped. Subject to the same rule
    /// as <see cref="CutDate"/>: a correction cannot introduce or remove the date, only fix it.
    /// </summary>
    public LocalDate? ReleasedDate { get; set; }
}
