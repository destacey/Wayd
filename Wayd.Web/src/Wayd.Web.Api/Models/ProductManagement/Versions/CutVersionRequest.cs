namespace Wayd.Web.Api.Models.ProductManagement.Versions;

/// <summary>
/// Freezes scope and marks a version ready to ship.
/// </summary>
public sealed record CutVersionRequest
{
    /// <summary>
    /// The moment scope was frozen — the build or tag that cut it. Supplied rather than taken from the
    /// clock, because cutting is often recorded after the fact. Required unless the deprecated
    /// <see cref="CutDate"/> is sent instead.
    /// </summary>
    public Instant? CutAt { get; set; }

    /// <summary>
    /// The day it was cut, read as 12:00 in the organization's default time zone.
    /// </summary>
    [Obsolete("Use CutAt. Removed in a future release.")]
    public LocalDate? CutDate { get; set; }

#pragma warning disable CS0618 // Reading the deprecated field is how it keeps working.
    public bool UsesLegacyDates() => CutDate is not null;

    public Instant ResolveCutAt(DateTimeZone zone) => CutAt ?? LegacyDeliveryDates.ToInstant(CutDate, zone)!.Value;
#pragma warning restore CS0618
}

public sealed class CutVersionRequestValidator : CustomValidator<CutVersionRequest>
{
    public CutVersionRequestValidator()
    {
#pragma warning disable CS0618
        RuleFor(r => r)
            .Must(r => r.CutAt is not null || r.CutDate is not null)
                .WithMessage("CutAt is required.")
            .Must(r => r.CutAt is null || r.CutDate is null)
                .WithMessage("Send CutAt or the deprecated CutDate, not both.");
#pragma warning restore CS0618
    }
}