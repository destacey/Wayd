namespace Wayd.Common.Application.SystemSettings.General.Dtos;

public sealed record GeneralSettingsDto
{
    /// <summary>
    /// The system time zone, as an IANA id: the zone a calendar day is read in wherever no team's own zone
    /// applies, and the zone new teams start with.
    /// </summary>
    public required string TimeZone { get; init; }
}
