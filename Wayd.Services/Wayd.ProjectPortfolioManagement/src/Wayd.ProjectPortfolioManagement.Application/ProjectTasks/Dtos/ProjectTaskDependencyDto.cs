using Wayd.Common.Application.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;

/// <summary>
/// DTO for a project task dependency.
/// </summary>
public sealed record ProjectTaskDependencyDto : IMapFrom<ProjectTaskDependency>
{
    public Guid Id { get; set; }
    public Guid PredecessorId { get; set; }
    public required ProjectTaskNavigationDto Predecessor { get; set; }
    public Guid SuccessorId { get; set; }
    public required ProjectTaskNavigationDto Successor { get; set; }
    public required SimpleNavigationDto Type { get; set; }
    public bool IsActive { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<ProjectTaskDependency, ProjectTaskDependencyDto>()
            .Map(dest => dest.Type, src => SimpleNavigationDto.FromEnum(src.Type))
            .Map(dest => dest.IsActive, src => src.IsActive);
    }
}
