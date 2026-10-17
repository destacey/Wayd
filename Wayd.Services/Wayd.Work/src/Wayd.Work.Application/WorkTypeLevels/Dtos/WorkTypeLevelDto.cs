using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Requests.WorkManagement.Interfaces;

namespace Wayd.Work.Application.WorkTypeLevels.Dtos;

public sealed record WorkTypeLevelDto : IMapFrom<WorkTypeLevel>, IWorkTypeLevelDto
{
    public int Id { get; set; }

    /// <summary>The name of the work type level.</summary>
    public required string Name { get; set; }

    /// <summary>The description of the work type level; null when it has none.</summary>
    public string? Description { get; set; }

    /// <summary>The tier the level belongs to: Portfolio, Requirement, Task or Other.</summary>
    public required SimpleNavigationDto Tier { get; set; }

    /// <summary>The level's position within its tier; levels sort by it in ascending order.</summary>
    public int Order { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<WorkTypeLevel, WorkTypeLevelDto>()
            .Map(dest => dest.Tier, src => SimpleNavigationDto.FromEnum(src.Tier));
    }
}
