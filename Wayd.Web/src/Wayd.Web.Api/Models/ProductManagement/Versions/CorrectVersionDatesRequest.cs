namespace Wayd.Web.Api.Models.ProductManagement.Versions;

/// <summary>
/// Corrects a version's recorded target, cut and released dates.
/// </summary>
/// <remarks>
/// All three dates are sent together, because the rule that the released date cannot precede the cut
/// date spans the pair and a correction commonly moves more than one. An omitted date is a cleared
/// date, not an unchanged one.
/// </remarks>
public sealed record CorrectVersionDatesRequest
{
    /// <summary>
    /// The corrected target date, or null to clear it. A target date is a statement of intent that was
    /// written down; correcting or removing it changes no lifecycle state.
    /// </summary>
    public LocalDate? TargetDate { get; set; }

    /// <summary>
    /// The corrected cut date, or null to clear it. May be added to a version that was never cut: a
    /// version can be marked released without being cut, so a cut date discovered later is a
    /// correction rather than a lifecycle step.
    /// </summary>
    public LocalDate? CutDate { get; set; }

    /// <summary>
    /// The corrected released date. May be added or changed, but not cleared on a version that has
    /// one — emptying it would leave the status contradicting the dates. Use the revert action to
    /// record that a version did not ship.
    /// </summary>
    public LocalDate? ReleasedDate { get; set; }
}
