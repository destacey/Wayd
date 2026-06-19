using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;

/// <summary>
/// Navigation DTO for a project task.
/// </summary>
public sealed record ProjectTaskNavigationDto : IMapFrom<ProjectTask>
{
    public Guid Id { get; set; }
    public required string Key { get; set; }
    public required string Name { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<ProjectTask, ProjectTaskNavigationDto>()
            .Map(dest => dest.Key, src => src.Key.Value);
    }
}
