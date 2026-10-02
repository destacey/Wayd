using Wayd.Common.Application.Dtos;
using Wayd.Planning.Application.PlanningSprints.Dtos;

namespace Wayd.Planning.Application.PlanningIntervals.Dtos;

/// <summary>
/// DTO for Planning Interval iteration with its mapped sprints.
/// </summary>
public sealed record PlanningIntervalIterationSprintsDto : IMapFrom<PlanningIntervalIteration>
{
    /// <summary>
    /// The primary identifier of the PI iteration.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The alternate key of the PI iteration.
    /// </summary>
    public int Key { get; set; }

    /// <summary>
    /// The name of the PI iteration.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The PI iteration start date.
    /// </summary>
    public required LocalDate Start { get; set; }

    /// <summary>
    /// The PI iteration end date.
    /// </summary>
    public required LocalDate End { get; set; }

    /// <summary>
    /// The PI iteration category.
    /// </summary>
    public required SimpleNavigationDto Category { get; set; }

    /// <summary>
    /// List of sprints mapped to this PI iteration.
    /// </summary>
    public List<PlanningSprintListDto> Sprints { get; set; } = [];

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<PlanningIntervalIteration, PlanningIntervalIterationSprintsDto>()
            .Map(dest => dest.Start, src => src.DateRange.Start)
            .Map(dest => dest.End, src => src.DateRange.End)
            .Map(dest => dest.Category, src => SimpleNavigationDto.FromEnum(src.Category))
            .Map(dest => dest.Sprints, src => src.IterationSprints.Select(itsp => itsp.Sprint));
    }
}
