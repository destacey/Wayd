using Wayd.Common.Application.Dtos;

namespace Wayd.Planning.Application.PlanningIntervals.Dtos;

public sealed record PlanningIntervalDetailsDto
{
    /// <summary>The planning interval's unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The planning interval's numeric key, an alternate to its id.</summary>
    public int Key { get; set; }

    /// <summary>
    /// The name of the planning interval.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>The planning interval's description; null when none is set.</summary>
    public string? Description { get; set; }

    /// <summary>The planning interval's first day.</summary>
    public required LocalDate Start { get; set; }

    /// <summary>The planning interval's last day, included in it.</summary>
    public required LocalDate End { get; set; }

    /// <summary>
    /// The current state of the Planning Interval.
    /// </summary>
    public required SimpleNavigationDto State { get; set; }

    /// <summary>
    /// Whether the planning interval's objectives are locked. While locked, objectives cannot be added or
    /// deleted, and an update keeps each objective's stored name and stretch flag, ignoring new values without
    /// an error; its description, status, progress and dates stay editable.
    /// </summary>
    public bool ObjectivesLocked { get; set; }

    public double? Predictability { get; set; }

    // TODO: do this with Mapster
    public static PlanningIntervalDetailsDto Create(PlanningInterval planningInterval, IDateTimeProvider dateTimeProvider)
    {
        var state = planningInterval.StateOn(dateTimeProvider.Now.InUtc().Date);
        return new PlanningIntervalDetailsDto()
        {
            Id = planningInterval.Id,
            Key = planningInterval.Key,
            Name = planningInterval.Name,
            Description = planningInterval.Description,
            Start = planningInterval.DateRange.Start,
            End = planningInterval.DateRange.End,
            State = SimpleNavigationDto.FromEnum(state),
            ObjectivesLocked = planningInterval.ObjectivesLocked,
            Predictability = planningInterval.CalculatePredictability(dateTimeProvider.Now.InUtc().Date)
        };
    }
}
