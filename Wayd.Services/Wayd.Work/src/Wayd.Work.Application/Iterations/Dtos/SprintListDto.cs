using Wayd.Common.Application.Dtos;
using Wayd.Work.Application.WorkTeams.Dtos;
using Wayd.Work.Domain.Models;

namespace Wayd.Work.Application.Iterations.Dtos;

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
    /// The sprint's state now, worked out from its actual and default dates when read.
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

    public required WorkTeamNavigationDto Team { get; set; }

    /// <summary>
    /// When the team started the sprint, if it did.
    /// </summary>
    public Instant? Started { get; set; }

    /// <summary>
    /// When the team completed the sprint, if it did.
    /// </summary>
    public Instant? Completed { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<Iteration, SprintListDto>()
            .Map(dest => dest.Start, src => src.DateRange.Start)
            .Map(dest => dest.End, src => src.DateRange.End)
            .Map(dest => dest.Team, src => src.Team)
            .Ignore(dest => dest.State);
    }
}
