using Wayd.Common.Domain.Models.Planning.Iterations;

namespace Wayd.Common.Application.Dtos;

/// <summary>
/// A day the whole team is off for some or all of, beyond its working week and holidays, such as a sprint's
/// offsite. It takes its share of everyone's capacity that day.
/// </summary>
public sealed record TeamDayOffDto
{
    /// <summary>The day the team is off.</summary>
    public required LocalDate Date { get; init; }

    /// <summary>The share of the working day the team is off, from 1 to 100: 100 is a full day off.</summary>
    public required int PercentOff { get; init; }

    /// <summary>The day as its DTO.</summary>
    public static TeamDayOffDto From(TeamDayOff day) => new() { Date = day.Date, PercentOff = day.PercentOff };
}
