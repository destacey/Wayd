using Wayd.Common.Application.Dtos;
using Wayd.Work.Application.WorkTeams.Dtos;

namespace Wayd.Work.Application.Iterations.Dtos;

public sealed record WorkIterationNavigationDto : NavigationDto, IMapFrom<Iteration>
{
    public WorkTeamNavigationDto? Team { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<Iteration, WorkIterationNavigationDto>()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.Key, src => src.Key)
            .Map(dest => dest.Name, src => src.Name)
            .Map(dest => dest.Team, src => src.Team);
    }
}
