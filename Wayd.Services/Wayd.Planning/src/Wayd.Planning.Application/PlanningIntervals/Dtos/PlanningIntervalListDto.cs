using Wayd.Common.Application.Dtos;

namespace Wayd.Planning.Application.PlanningIntervals.Dtos;

public sealed record PlanningIntervalListDto
{
    /// <summary>The planning interval's unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The planning interval's numeric key, an alternate to its id.</summary>
    public int Key { get; set; }

    /// <summary>
    /// The name of the planning interval.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>The planning interval's first day.</summary>
    public LocalDate Start { get; set; }

    /// <summary>The planning interval's last day, included in it.</summary>
    public LocalDate End { get; set; }

    /// <summary>
    /// The current state of the Planning Interval.
    /// </summary>
    public required SimpleNavigationDto State { get; set; }

    // TODO: do this with Mapster
    public static PlanningIntervalListDto Create(PlanningInterval planningInterval, IDateTimeProvider dateTimeProvider)
    {
        var state = planningInterval.StateOn(dateTimeProvider.Now.InUtc().Date);
        return new PlanningIntervalListDto()
        {
            Id = planningInterval.Id,
            Key = planningInterval.Key,
            Name = planningInterval.Name,
            Start = planningInterval.DateRange.Start,
            End = planningInterval.DateRange.End,
            State = SimpleNavigationDto.FromEnum(state)
        };
    }
}
