using System.ComponentModel.DataAnnotations;

namespace Wayd.Organization.Application.HolidayCalendars.Dtos;

/// <summary>A holiday calendar in a list.</summary>
public sealed record HolidayCalendarListDto
{
    [Required]
    public Guid Id { get; init; }

    [Required]
    public int Key { get; init; }

    [Required]
    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>How many holidays the calendar holds, across all years.</summary>
    [Required]
    public int HolidayCount { get; init; }

    /// <summary>Whether this is the system default, used by every team operating model that names no calendar.</summary>
    [Required]
    public bool IsDefault { get; init; }
}
