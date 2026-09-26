namespace Wayd.Web.Api.Models.ProductManagement.Versions;

/// <summary>
/// Corrects a version's recorded target date and cut and released moments.
/// </summary>
/// <remarks>
/// All three are sent together, because the rule that a version cannot be released before it was cut
/// spans the pair and a correction commonly moves more than one. An omitted value is a cleared one,
/// not an unchanged one.
/// </remarks>
public sealed record CorrectVersionDatesRequest
{
    /// <summary>
    /// The corrected target date, or null to clear it. A target date is a statement of intent that was
    /// written down; correcting or removing it changes no lifecycle state.
    /// </summary>
    public LocalDate? TargetDate { get; set; }

    /// <summary>
    /// The corrected cut moment, or null to clear it. May be added to a version that was never cut: a
    /// version can be marked released without being cut, so a cut moment discovered later is a
    /// correction rather than a lifecycle step.
    /// </summary>
    public Instant? CutAt { get; set; }

    /// <summary>
    /// The corrected released moment. May be added or changed, but not cleared on a version that has
    /// one — emptying it would leave the status contradicting the record. Use the revert action to
    /// record that a version did not ship.
    /// </summary>
    public Instant? ReleasedAt { get; set; }
}
