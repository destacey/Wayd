using System.ComponentModel.DataAnnotations;
using NodaTime;

namespace Wayd.Organization.Application.Teams.Dtos;

/// <summary>
/// The values a new operating model is pre-filled with. They are suggestions only: a saved model never reads
/// through to its parent or to the system settings.
/// </summary>
public sealed record OperatingModelDefaultsDto
{
    /// <summary>
    /// The IANA id of the parent team of teams' time zone on the start date, or the system default when the
    /// team had no parent with an operating model then.
    /// </summary>
    [Required]
    public required string TimeZone { get; init; }

    /// <summary>
    /// The name of the team of teams <see cref="TimeZone"/> was taken from, or null when it is the system default.
    /// </summary>
    public string? TimeZoneSource { get; init; }

    /// <summary>
    /// The system default commitment grace period in days.
    /// </summary>
    [Required]
    public int CommitmentGraceDays { get; init; }

    /// <summary>
    /// The system default working days, Monday first.
    /// </summary>
    [Required]
    public required IReadOnlyList<IsoDayOfWeek> WorkingDays { get; init; }
}
