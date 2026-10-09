using System.ComponentModel.DataAnnotations;
using NodaTime;

namespace Wayd.Organization.Application.HolidayCalendars.Dtos;

/// <summary>A day off in a holiday calendar.</summary>
public sealed record HolidayDto
{
    [Required]
    public Guid Id { get; init; }

    [Required]
    public LocalDate Date { get; init; }

    [Required]
    public required string Name { get; init; }
}
