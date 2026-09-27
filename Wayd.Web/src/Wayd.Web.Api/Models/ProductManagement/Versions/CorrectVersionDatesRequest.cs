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

    /// <summary>The cut day, read as 12:00 in the organization's default time zone.</summary>
    [Obsolete("Use CutAt. Removed in a future release.")]
    public LocalDate? CutDate { get; set; }

    /// <summary>The released day, read as 12:00 in the organization's default time zone.</summary>
    [Obsolete("Use ReleasedAt. Removed in a future release.")]
    public LocalDate? ReleasedDate { get; set; }

#pragma warning disable CS0618 // Reading the deprecated fields is how they keep working.
    public bool UsesLegacyDates() => CutDate is not null || ReleasedDate is not null;

    public Instant? ResolveCutAt(DateTimeZone zone) => CutAt ?? LegacyDeliveryDates.ToInstant(CutDate, zone);

    public Instant? ResolveReleasedAt(DateTimeZone zone) => ReleasedAt ?? LegacyDeliveryDates.ToInstant(ReleasedDate, zone);
#pragma warning restore CS0618
}

public sealed class CorrectVersionDatesRequestValidator : CustomValidator<CorrectVersionDatesRequest>
{
    public CorrectVersionDatesRequestValidator()
    {
#pragma warning disable CS0618
        RuleFor(r => r)
            .Must(r => r.CutAt is null || r.CutDate is null)
                .WithMessage("Send CutAt or the deprecated CutDate, not both.")
            .Must(r => r.ReleasedAt is null || r.ReleasedDate is null)
                .WithMessage("Send ReleasedAt or the deprecated ReleasedDate, not both.");
#pragma warning restore CS0618
    }
}