using Wayd.Work.Application.WorkProjects.Dtos;

namespace Wayd.Work.Application.WorkItems.Dtos;

public class WorkItemProjectInfoDto : IMapFrom<WorkItem>
{
    public Guid Id { get; set; }
    public required string Key { get; set; }
    public bool HasParent { get; set; }
    public WorkProjectNavigationDto? Project { get; set; }
    public WorkProjectNavigationDto? ParentProject { get; set; }
    public required string ProjectSource { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<WorkItem, WorkItemProjectInfoDto>()
            .Map(dest => dest.Key, src => src.Key.ToString())
            .Map(dest => dest.HasParent, src => src.ParentId.HasValue)
            .Map(dest => dest.Project, src => src.Project != null
                ? src.Project
                : null)
            .Map(dest => dest.ParentProject, src => src.ParentProject != null
                ? src.ParentProject
                : null)
            .Map(dest => dest.ProjectSource, src => src.ProjectId.HasValue
                ? WorkItemDetailsDto.WorkItemProjectSource
                : src.ParentProjectId.HasValue
                    ? WorkItemDetailsDto.ParentProjectSource
                    : WorkItemDetailsDto.NoProjectSource);
    }
}
