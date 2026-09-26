using Wayd.Common.Application.Dtos;
using Wayd.Planning.Application.Models;
using Wayd.Planning.Domain.Models.Iterations;

namespace Wayd.Planning.Application.Iterations.Dtos;

public sealed record SprintListDto : IMapFrom<Iteration>
{
    public Guid Id { get; set; }

    /// <summary>
    /// The unique key of the sprint.  This is an alternate key to the Id.
    /// </summary>
    public int Key { get; set; }

    /// <summary>
    /// The name of the sprint.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The current state of the sprint.
    /// </summary>
    public required SimpleNavigationDto State { get; set; }

    /// <summary>
    /// The first planned day of the sprint.
    /// </summary>
    public LocalDate Start { get; set; }

    /// <summary>
    /// The last planned day of the sprint, included in it.
    /// </summary>
    public LocalDate End { get; set; }

    public required PlanningTeamNavigationDto Team { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<Iteration, SprintListDto>()
            .Map(dest => dest.State, src => SimpleNavigationDto.FromEnum(src.State))
            .Map(dest => dest.Start, src => src.DateRange.Start)
            .Map(dest => dest.End, src => src.DateRange.End)
            .Map(dest => dest.Team, src => PlanningTeamNavigationDto.FromPlanningTeam(src.Team!));
    }
}
