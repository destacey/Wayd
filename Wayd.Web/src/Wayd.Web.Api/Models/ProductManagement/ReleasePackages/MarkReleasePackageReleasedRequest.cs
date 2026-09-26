namespace Wayd.Web.Api.Models.ProductManagement.ReleasePackages;

/// <summary>
/// Records that a package shipped.
/// </summary>
public sealed record MarkReleasePackageReleasedRequest
{
    /// <summary>
    /// The moment it shipped. Supplied rather than taken from the clock, because shipping is often
    /// recorded after the fact. Required unless the deprecated <see cref="ReleasedDate"/> is sent instead.
    /// </summary>
    public Instant? ReleasedAt { get; set; }

    /// <summary>
    /// The day it shipped, read as 12:00 in the organization's default time zone.
    /// </summary>
    [Obsolete("Use ReleasedAt. Removed in a future release.")]
    public LocalDate? ReleasedDate { get; set; }

#pragma warning disable CS0618 // Reading the deprecated field is how it keeps working.
    public bool UsesLegacyDates() => ReleasedDate is not null;

    public Instant ResolveReleasedAt(DateTimeZone zone) =>
        ReleasedAt ?? LegacyDeliveryDates.ToInstant(ReleasedDate, zone)!.Value;
#pragma warning restore CS0618
}

public sealed class MarkReleasePackageReleasedRequestValidator : CustomValidator<MarkReleasePackageReleasedRequest>
{
    public MarkReleasePackageReleasedRequestValidator()
    {
#pragma warning disable CS0618
        RuleFor(r => r)
            .Must(r => r.ReleasedAt is not null || r.ReleasedDate is not null)
                .WithMessage("ReleasedAt is required.")
            .Must(r => r.ReleasedAt is null || r.ReleasedDate is null)
                .WithMessage("Send ReleasedAt or the deprecated ReleasedDate, not both.");
#pragma warning restore CS0618
    }
}