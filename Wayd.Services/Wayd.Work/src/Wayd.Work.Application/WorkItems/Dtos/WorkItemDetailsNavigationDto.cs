using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Work.Application.Iterations.Dtos;
using Wayd.Work.Application.WorkTeams.Dtos;

namespace Wayd.Work.Application.WorkItems.Dtos;

public sealed record WorkItemDetailsNavigationDto : IMapFrom<WorkItem>
{
    public required Guid Id { get; set; }
    public required string Key { get; set; }
    public required string Title { get; set; }
    public required string WorkspaceKey { get; set; }
    public required string Type { get; set; }
    public required string Status { get; set; }
    public required SimpleNavigationDto StatusCategory { get; set; }
    public WorkTeamNavigationDto? Team { get; set; }
    public WorkIterationNavigationDto? Sprint { get; set; }
    public Instant? ActivatedTimestamp { get; set; }
    public Instant? DoneTimestamp { get; set; }
    public string? ExternalViewWorkItemUrl { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<WorkItem, WorkItemDetailsNavigationDto>()
            .Map(dest => dest.WorkspaceKey, src => src.Workspace.Key)
            .Map(dest => dest.Type, src => src.Type.Name)
            .Map(dest => dest.Status, src => src.Status.Name)
            .Map(dest => dest.StatusCategory, src => SimpleNavigationDto.FromEnum(src.StatusCategory))
            .Map(dest => dest.Sprint, src => src.Iteration != null && src.Iteration.Type == IterationType.Sprint ? src.Iteration : null)
            .Map(dest => dest.ExternalViewWorkItemUrl, src => src.Workspace.ExternalViewWorkItemUrlTemplate == null ? null : $"{src.Workspace.ExternalViewWorkItemUrlTemplate}{src.ExternalId}");
    }
}
