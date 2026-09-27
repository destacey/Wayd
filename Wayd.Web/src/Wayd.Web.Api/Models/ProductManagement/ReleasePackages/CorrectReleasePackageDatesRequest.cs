namespace Wayd.Web.Api.Models.ProductManagement.ReleasePackages;

/// <summary>
/// Corrects a package's recorded target date and released moment.
/// </summary>
/// <remarks>
/// Both are sent, so an omitted target date is cleared. The released moment can be changed on a released
/// package but not cleared, and cannot be added to one that has not been released.
/// </remarks>
public sealed record CorrectReleasePackageDatesRequest
{
    public LocalDate? TargetDate { get; set; }
    public Instant? ReleasedAt { get; set; }

    /// <summary>The released day, read as 12:00 in the organization's default time zone.</summary>
    [Obsolete("Use ReleasedAt. Removed in a future release.")]
    public LocalDate? ReleasedDate { get; set; }

#pragma warning disable CS0618 // Reading the deprecated field is how it keeps working.
    public bool UsesLegacyDates() => ReleasedDate is not null;

    public Instant? ResolveReleasedAt(DateTimeZone zone) =>
        ReleasedAt ?? LegacyDeliveryDates.ToInstant(ReleasedDate, zone);
#pragma warning restore CS0618
}

public sealed class CorrectReleasePackageDatesRequestValidator : CustomValidator<CorrectReleasePackageDatesRequest>
{
    public CorrectReleasePackageDatesRequestValidator()
    {
#pragma warning disable CS0618
        RuleFor(r => r)
            .Must(r => r.ReleasedAt is null || r.ReleasedDate is null)
                .WithMessage("Send ReleasedAt or the deprecated ReleasedDate, not both.");
#pragma warning restore CS0618
    }
}