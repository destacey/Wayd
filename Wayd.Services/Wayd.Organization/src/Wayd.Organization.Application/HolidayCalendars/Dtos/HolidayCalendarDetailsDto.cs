using System.ComponentModel.DataAnnotations;

namespace Wayd.Organization.Application.HolidayCalendars.Dtos;

/// <summary>A holiday calendar with its holidays.</summary>
public sealed record HolidayCalendarDetailsDto
{
    [Required]
    public Guid Id { get; init; }

    [Required]
    public int Key { get; init; }

    [Required]
    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>Whether this is the system default, used by every team operating model that names no calendar.</summary>
    [Required]
    public bool IsDefault { get; init; }

    /// <summary>How many team operating models name this calendar. A calendar in use cannot be deleted.</summary>
    [Required]
    public int OperatingModelCount { get; init; }

    /// <summary>The holidays, in date order.</summary>
    [Required]
    public required IReadOnlyList<HolidayDto> Holidays { get; init; }
}
