using Wayd.Common.Application.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Dtos;

public sealed record ProjectLifecycleListDto : IMapFrom<ProjectLifecycle>
{
    public Guid Id { get; set; }
    public int Key { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required SimpleNavigationDto State { get; set; }
    public int PhaseCount { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<ProjectLifecycle, ProjectLifecycleListDto>()
            .Map(dest => dest.State, src => SimpleNavigationDto.FromEnum(src.State))
            .Map(dest => dest.PhaseCount, src => src.Phases.Count);
    }
}
