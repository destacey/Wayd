namespace Wayd.Common.Application.TimeZones.Dtos;

public sealed record TimeZoneDto
{
    /// <summary>The IANA time zone id, e.g. "America/Chicago".</summary>
    public required string Id { get; init; }

    /// <summary>The zone's offset from UTC right now, e.g. "-05:00". Changes with daylight saving time.</summary>
    public required string CurrentOffset { get; init; }
}
